using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Suruga.Audio.Primitives;
using Suruga.Persistence;
using Suruga.Primitives;

namespace Suruga.Audio;

internal sealed class TrackQueue
{
    internal IReadOnlyList<Track> Next => _currentIndex >= 0
        ? _tracks[(_currentIndex + 1)..]
        : ReadOnlyCollection<Track>.Empty;

    internal IReadOnlyList<Track> Previous => _currentIndex > 0
        ? _tracks[.._currentIndex]
        : ReadOnlyCollection<Track>.Empty;

    internal Track? CurrentTrack => _currentIndex >= 0 ? _tracks[_currentIndex] : null;
    
    internal bool HasCurrent => _currentIndex >= 0;

    internal bool HasNext => LoopMode switch
    {
        LoopMode.Track => _currentIndex >= 0,
        LoopMode.Queue => _currentIndex >= 0 && _tracks.Count > 0,
        _ => _currentIndex >= 0 && _currentIndex < _tracks.Count - 1
    };
    
    internal bool HasPrevious => _currentIndex > 0;

    internal LoopMode LoopMode { get; set; }

    private readonly TrackQueueStateRepository _repository;
    private readonly ulong _guildId;
    
    private readonly List<Track> _tracks;
    
    private int _currentIndex;

    internal TrackQueue(TrackQueueStateRepository repository, ulong guildId)
    {
        _repository = repository;
        _guildId = guildId;
        
        TrackQueueState? queueState = _repository.Load(guildId);
        
        _tracks = queueState?.Tracks ?? [];
        _currentIndex = queueState?.CurrentIndex ?? -1;
        LoopMode = queueState?.LoopMode ?? LoopMode.None;
    }

    internal void Add(Track track)
    {
        _tracks.Add(track);
        
        if (_currentIndex < 0)
        {
            _currentIndex = 0;
        }
    }

    internal bool TryGetCurrent([NotNullWhen(true)] out Track? track)
    {
        if (_currentIndex < 0)
        {
            track = null;
            return false;
        }
        
        track = _tracks[_currentIndex];
        return true;
    }

    internal bool TryMoveToNext(bool force = false)
    {
        if (_currentIndex < 0)
        {
            return false;
        }
        
        if (LoopMode is LoopMode.Track && !force)
        {
            return true; // Leave the current track as is.
        }

        // Set the current track back to the first node if the current track is the
        // last track playing and loop mode is set to per-queue.
        if (LoopMode is LoopMode.Queue && _currentIndex == _tracks.Count - 1)
        {
            _currentIndex = _tracks.Count > 0 ? 0 : -1;
            return _currentIndex >= 0;
        }
        
        if (_currentIndex < _tracks.Count - 1)
        {
            _currentIndex++;
            return true;
        }

        return false;
    }

    internal bool TryMoveToPrevious()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            return true;
        }
        
        return false;
    }

    internal bool TryShuffle()
    {
        int count = _tracks.Count;

        if (count < 2)
        {
            return false;
        }

        Track? currentTrack = _currentIndex >= 0 ? _tracks[_currentIndex] : null;
        List<Track> shuffled = _tracks.Shuffle().ToList();

        _tracks.Clear();
        _tracks.AddRange(shuffled);

        _currentIndex = currentTrack is not null
            ? _tracks.IndexOf(currentTrack)
            : -1;

        return true;
    }

    internal bool TryClear()
    {
        if (_tracks.Count == 0)
        {
            return false;
        }
        
        _tracks.Clear();
        _currentIndex = -1;
        
        return true;
    }

    internal async Task SaveAsync(CancellationToken token = default)
    {
        await _repository.SaveAsync(new TrackQueueState
        {
            GuildId = _guildId,
            Tracks = _tracks,
            CurrentIndex = _currentIndex,
            LoopMode = LoopMode
        }, token);
    }
}
