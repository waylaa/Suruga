using System.Diagnostics.CodeAnalysis;
using Suruga.Audio.Primitives;
using Suruga.Primitives;

namespace Suruga.Audio;

internal sealed class TrackQueue
{
    internal IReadOnlyList<Track> Next
    {
        get
        {
            using (_lock.EnterScope())
            {
                if (_currentIndex < 0 || _isPastEnd)
                {
                    return [];
                }

                return _tracks.GetRange(_currentIndex + 1, _tracks.Count - _currentIndex - 1);
            }
        }
    }

    internal IReadOnlyList<Track> Previous
    {
        get
        {
            using (_lock.EnterScope())
            {
                if (_currentIndex < 0)
                {
                    return _history.AsReadOnly();
                }

                // While past the end, the last track is history too; otherwise
                // everything strictly before the current index is history.
                int exclusiveEnd = _isPastEnd ? _currentIndex + 1 : _currentIndex;

                List<Track> previousTracks = new(_history.Count + exclusiveEnd);
                previousTracks.AddRange(_history);
                previousTracks.AddRange(_tracks.GetRange(0, exclusiveEnd));

                return previousTracks;
            }
        }
    }

    internal bool HasNextTrack
    {
        get
        {
            using (_lock.EnterScope())
            {
                if (_currentIndex < 0 || _isPastEnd)
                {
                    return false;
                }

                // Track/Queue loop modes always have a "next" - either the same
                // track again or a wrap back to the start.
                return LoopMode is LoopMode.Track or LoopMode.Queue || _currentIndex < _tracks.Count - 1;
            }
        }
    }

    internal bool HasPreviousTrack
    {
        get
        {
            using (_lock.EnterScope())
            {
                if (_history.Count > 0)
                {
                    return true;
                }

                return _isPastEnd ? _currentIndex >= 0 : _currentIndex > 0;
            }
        }
    }

    internal Track? CurrentTrack
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _isPastEnd || _currentIndex < 0 ? null : _tracks[_currentIndex];
            }
        }
    }

    internal LoopMode LoopMode
    {
        get
        {
            using (_lock.EnterScope())
            {
                return field;
            }
        }
        set
        {
            using (_lock.EnterScope())
            {
                field = value;
            }
        }
    } = LoopMode.None;

    private readonly List<Track> _tracks = [];

    // Tracks from laps that have already fully completed under LoopMode.Queue.
    // _tracks/_currentIndex only cover the current lap, since looping restarts
    // the same list.
    private readonly List<Track> _history = [];

    private readonly Lock _lock = new();

    // Index of the current track within _tracks, or -1 if the queue is empty.
    private int _currentIndex = -1;

    // True once playback has advanced past the last track in a non-looping queue.
    // _currentIndex is left pointing at the last track rather than being reset
    // so Previous/HasPreviousTrack can still report it as history.
    private bool _isPastEnd;
    
    private const int MaxHistorySize = 100;
    private const int MaxSnapshotSize = 100;

    internal TrackQueue(TrackQueueSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.Tracks.Count == 0)
        {
            return;
        }

        LoopMode = snapshot.LoopMode;
        _tracks.AddRange(snapshot.Tracks);

        // If the index is ever out of bounds, default to the first item.
        _currentIndex = snapshot.CurrentTrackIndex >= 0 && snapshot.CurrentTrackIndex < _tracks.Count
            ? snapshot.CurrentTrackIndex
            : 0;
    }

    internal void Add(Track track)
    {
        using (_lock.EnterScope())
        {
            _tracks.Add(track);

            if (_currentIndex < 0)
            {
                _currentIndex = 0;
            }
            else if (_isPastEnd)
            {
                // Playback had already finished with _currentIndex left on the
                // last-played track. Advance to the newly queued track instead
                // of resuming by replaying that finished one.
                _currentIndex = _tracks.Count - 1;
            }

            _isPastEnd = false;
        }
    }

    internal bool TryMoveToNext([NotNullWhen(true)] out Track? track)
    {
        using (_lock.EnterScope())
        {
            if (_currentIndex < 0)
            {
                track = null;
                return false;
            }

            if (LoopMode is LoopMode.Track)
            {
                _isPastEnd = false;
                track = _tracks[_currentIndex];
                return true;
            }

            int nextIndex = _currentIndex + 1;

            if (nextIndex >= _tracks.Count)
            {
                if (LoopMode is not LoopMode.Queue)
                {
                    // Leave _currentIndex on the last-played track so the
                    // completed track still shows up in Previous/history.
                    _isPastEnd = true;
                    track = null;
                    return false;
                }

                // The whole current lap is about to be replayed from the top,
                // archive it as permanent history first so Previous/Next don't
                // reset on wrap.
                _history.AddRange(_tracks);
                
                if (_history.Count > MaxHistorySize)
                {
                    _history.RemoveRange(0, _history.Count - MaxHistorySize);
                }

                nextIndex = 0;
            }

            _currentIndex = nextIndex;
            _isPastEnd = false;
            track = _tracks[nextIndex];

            return true;
        }
    }

    internal bool TryMoveToPrevious([NotNullWhen(true)] out Track? track)
    {
        using (_lock.EnterScope())
        {
            if (_tracks.Count == 0)
            {
                track = null;
                return false;
            }

            if (_isPastEnd)
            {
                _isPastEnd = false;
                track = _tracks[_currentIndex];
                return true;
            }

            if (_currentIndex > 0)
            {
                _currentIndex--;
                track = _tracks[_currentIndex];
                return true;
            }

            // Rewinding off the front of the current lap, restore the last track
            // from the previous lap.
            if (_currentIndex == 0 && _history.Count > 0)
            {
                Track restoredTrack = _history[^1];
                _history.RemoveAt(_history.Count - 1);

                // The restored track is already the last track in _tracks because
                // the previous lap was copied into _history when it wrapped.
                // Move it from the end to the beginning instead of inserting a duplicate.
                _tracks.RemoveAt(_tracks.Count - 1);
                _tracks.Insert(0, restoredTrack);

                _currentIndex = 0;
                track = restoredTrack;

                return true;
            }

            track = null;
            return false;
        }
    }

    internal bool Shuffle()
    {
        using (_lock.EnterScope())
        {
            int startIndex = _currentIndex < 0 ? 0 : _currentIndex + 1;
            int remainingCount = _tracks.Count - startIndex;

            // Cannot shuffle with less than 2 upcoming items.
            if (remainingCount < 2)
            {
                return false;
            }

            // Fisher-Yates shuffle over the upcoming tracks only.
            for (int i = remainingCount - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (_tracks[startIndex + i], _tracks[startIndex + j]) = (_tracks[startIndex + j], _tracks[startIndex + i]);
            }

            return true;
        }
    }

    internal TrackQueueSnapshot GetSnapshot()
    {
        using (_lock.EnterScope())
        {
            int count = Math.Min(_tracks.Count, MaxSnapshotSize);
            List<Track> snapshotTracks = _tracks.GetRange(0, count);

            int currentIndex = _currentIndex >= 0 && _currentIndex < count ? _currentIndex : -1;
            return new TrackQueueSnapshot(snapshotTracks.AsReadOnly(), currentIndex, LoopMode);
        }
    }

    internal bool Clear()
    {
        using (_lock.EnterScope())
        {
            if (_tracks.Count == 0)
            {
                return false;
            }

            // Keep the currently playing track (if any) so it still shows up as
            // 'now playing' afterward. The rest of the queue gets wiped.
            Track? currentTrack = !_isPastEnd && _currentIndex >= 0 ? _tracks[_currentIndex] : null;

            // Nothing to clear if the only track left is the one currently playing.
            if (currentTrack is not null && _tracks.Count <= 1)
            {
                return false;
            }

            _tracks.Clear();
            _history.Clear();
            _isPastEnd = false;

            if (currentTrack is not null)
            {
                _tracks.Add(currentTrack);
                _currentIndex = 0;
            }
            else
            {
                _currentIndex = -1;
            }

            return true;
        }
    }

    internal sealed record TrackQueueSnapshot(IReadOnlyList<Track> Tracks, int CurrentTrackIndex, LoopMode LoopMode);
}
