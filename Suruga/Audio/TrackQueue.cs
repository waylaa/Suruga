using System.Collections;
using Suruga.Audio.Primitives;
using Suruga.Primitives;

namespace Suruga.Audio;

internal sealed class TrackQueue : IReadOnlyList<Track>
{
    internal IReadOnlyList<Track> QueuedTracks
    {
        get
        {
            using (_lock.EnterScope())
            {
                if (_currentIndex < 0 || _currentIndex >= _tracks.Count - 1)
                {
                    return [];
                }

                return _tracks
                    .GetRange(_currentIndex + 1, _tracks.Count - _currentIndex - 1)
                    .AsReadOnly();
            }
        }
    }
    
    internal IReadOnlyList<Track> History
    {
        get
        {
            using (_lock.EnterScope())
            {
                if (_currentIndex <= 0)
                {
                    return [];
                }

                int count = Math.Min(_currentIndex, _tracks.Count);
                return _tracks.GetRange(0, count).AsReadOnly();
            }
        }
    }

    internal LoopMode LoopMode
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _loopMode;
            }
        }
        set
        {
            using (_lock.EnterScope())
            {
                _loopMode = value;
            }
        }
    }

    internal Track? CurrentTrack
    {
        get
        {
            using (_lock.EnterScope())
            {
                return GetCurrentTrackUnsafe();
            }
        }
    }
    
    internal bool HasNextTrack
    {
        get
        {
            using (_lock.EnterScope())
            {
                return HasNextTrackUnsafe();
            }
        }
    }

    public int Count
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _tracks.Count;
            }
        }
    }
    
    public Track this[int index]
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _tracks[index];
            }
        }
    }
    
    private readonly Lock _lock = new();
    private readonly List<Track> _tracks;

    // -1  = queue contains tracks but playback has not started.
    // 0..Count-1 = current track.
    // Count = playback has exhausted the queue.
    private int _currentIndex;

    private LoopMode _loopMode;

    internal TrackQueue(TrackQueueSnapshot? snapshot = null)
    {
        if (snapshot is null)
        {
            _tracks = [];
            _currentIndex = -1;
            _loopMode = LoopMode.None;
            
            return;
        }

        _tracks = new List<Track>(snapshot.Tracks);
        _loopMode = snapshot.LoopMode;

        _currentIndex = NormalizeLoadedIndex(snapshot.CurrentTrackIndex, _tracks.Count);
    }

    public IEnumerator<Track> GetEnumerator()
    {
        Track[] snapshot;

        using (_lock.EnterScope())
        {
            snapshot = _tracks.ToArray();
        }

        return ((IEnumerable<Track>)snapshot).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    internal void Add(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);

        using (_lock.EnterScope())
        {
            bool wasEmpty = _tracks.Count == 0;
            bool wasExhausted = _currentIndex == _tracks.Count;

            _tracks.Add(track);

            /*
             * Empty queue:
             *
             *     [] + A -> [A]
             *             ^
             *
             * Start immediately from the first track.
             */
            if (wasEmpty)
            {
                _currentIndex = 0;
                return;
            }

            /*
             * Exhausted queue:
             *
             *     [A B] -> exhausted -> [A B C]
             *                              ^
             *
             * The newly-added track becomes the playable current
             * track rather than resurrecting A.
             */
            if (wasExhausted)
            {
                _currentIndex = _tracks.Count - 1;
            }
        }
    }

    internal void AddRange(IEnumerable<Track> tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);

        Track[] items = tracks.ToArray();

        if (items.Length == 0)
        {
            return;
        }

        using (_lock.EnterScope())
        {
            bool wasEmpty = _tracks.Count == 0;
            bool wasExhausted = _currentIndex == _tracks.Count;

            _tracks.AddRange(items);

            if (wasEmpty)
            {
                _currentIndex = 0;
            }
            else if (wasExhausted)
            {
                _currentIndex = _tracks.Count - items.Length;
            }
        }
    }

    internal bool TryMoveToNext(out Track? track)
    {
        using (_lock.EnterScope())
        {
            if (_tracks.Count == 0)
            {
                track = null;
                return false;
            }

            /*
             * Manual "next" always advances. Track looping only
             * affects automatic advancement after completion.
             */
            if (_currentIndex == _tracks.Count)
            {
                if (_loopMode == LoopMode.Queue)
                {
                    _currentIndex = 0;
                    track = _tracks[0];
                    return true;
                }

                track = null;
                return false;
            }

            if (_currentIndex + 1 < _tracks.Count)
            {
                _currentIndex++;
                track = _tracks[_currentIndex];
                return true;
            }

            if (_loopMode == LoopMode.Queue)
            {
                _currentIndex = 0;
                track = _tracks[0];
                return true;
            }

            _currentIndex = _tracks.Count;
            track = null;
            return false;
        }
    }

    internal bool TryMoveToPrevious(out Track? track)
    {
        using (_lock.EnterScope())
        {
            if (_tracks.Count == 0)
            {
                track = null;
                return false;
            }

            // If playback has exhausted the queue, the previous
            // track is the last track that actually played.
            if (_currentIndex == _tracks.Count)
            {
                _currentIndex = _tracks.Count - 1;
                track = _tracks[_currentIndex];
                return true;
            }

            if (_currentIndex > 0)
            {
                _currentIndex--;
                track = _tracks[_currentIndex];
                return true;
            }

            if (_loopMode == LoopMode.Queue)
            {
                _currentIndex = _tracks.Count - 1;
                track = _tracks[_currentIndex];
                return true;
            }

            track = null;
            return false;
        }
    }
    
    internal bool TryAdvanceAfterCompletion(out Track? track)
    {
        using (_lock.EnterScope())
        {
            if (_tracks.Count == 0)
            {
                track = null;
                return false;
            }

            if (_currentIndex < 0)
            {
                _currentIndex = 0;
                track = _tracks[0];
                return true;
            }

            if (_currentIndex >= _tracks.Count)
            {
                track = null;
                return false;
            }

            switch (_loopMode)
            {
                case LoopMode.Track:
                    track = _tracks[_currentIndex];
                    return true;

                case LoopMode.Queue:
                {
                    _currentIndex++;
                    
                    if (_currentIndex >= _tracks.Count)
                    {
                        _currentIndex = 0;
                    }

                    track = _tracks[_currentIndex];
                    return true;
                }
                
                case LoopMode.None:
                default:
                {
                    _currentIndex++;
                    
                    if (_currentIndex >= _tracks.Count)
                    {
                        _currentIndex = _tracks.Count;
                        track = null;
                        return false;
                    }

                    track = _tracks[_currentIndex];
                    return true;
                }
            }
        }
    }

    internal bool Shuffle()
    {
        using (_lock.EnterScope())
        {
            if (_tracks.Count < 2)
            {
                return false;
            }

            /*
             * Keep the current track fixed.
             *
             * Only upcoming tracks are shuffled. This means:
             *
             *     A [B C D E]
             *
             * can become:
             *
             *     A [D B E C]
             *
             * while Previous still means B -> A rather than some
             * randomly shuffled history.
             */
            int firstUpcoming;

            if (_currentIndex < 0)
            {
                firstUpcoming = 0;
            }
            else if (_currentIndex >= _tracks.Count)
            {
                /*
                 * Exhausted queue: shuffle the complete queue and
                 * remain exhausted.
                 */
                ShuffleRangeUnsafe(0, _tracks.Count);
                return true;
            }
            else
            {
                firstUpcoming = _currentIndex + 1;
            }

            int count = _tracks.Count - firstUpcoming;

            if (count < 2)
            {
                return false;
            }

            ShuffleRangeUnsafe(firstUpcoming, count);
            return true;
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

            _tracks.Clear();
            _currentIndex = -1;

            return true;
        }
    }
    
    internal void Reset()
    {
        using (_lock.EnterScope())
        {
            if (_tracks.Count == 0)
            {
                _currentIndex = -1;
                return;
            }

            _currentIndex = 0;
        }
    }

    internal TrackQueueSnapshot GetSnapshot()
    {
        using (_lock.EnterScope())
        {
            return new TrackQueueSnapshot(_tracks.ToList().AsReadOnly(), _currentIndex, _loopMode);
        }
    }

    private Track? GetCurrentTrackUnsafe()
        => (uint)_currentIndex >= (uint)_tracks.Count ? null : _tracks[_currentIndex];

    private bool HasNextTrackUnsafe()
    {
        if (_tracks.Count == 0)
        {
            return false;
        }

        if (_currentIndex < 0)
        {
            return _tracks.Count > 0;
        }

        if (_currentIndex >= _tracks.Count)
        {
            return _loopMode == LoopMode.Queue;
        }

        if (_currentIndex + 1 < _tracks.Count)
        {
            return true;
        }

        return _loopMode == LoopMode.Queue;
    }

    private void ShuffleRangeUnsafe(int start, int count)
    {
        for (int i = count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);

            int left = start + i;
            int right = start + j;

            (_tracks[left], _tracks[right]) = (_tracks[right], _tracks[left]);
        }
    }

    private static int NormalizeLoadedIndex(int index, int count)
    {
        if (count == 0)
        {
            return -1;
        }
        
        // Old persisted queues may have used -1 for 'no current'.
        if (index < 0)
        {
            return 0;
        }
        
        // Count is a valid-persisted state meaning 'exhausted'.
        if (index > count)
        {
            return count;
        }

        return index;
    }

    internal sealed record TrackQueueSnapshot(IReadOnlyList<Track> Tracks, int CurrentTrackIndex, LoopMode LoopMode);
}
