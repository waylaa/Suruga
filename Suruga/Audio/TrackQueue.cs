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
                if (_currentNode is null)
                {
                    return _tracks.ToList().AsReadOnly();
                }

                List<Track> nextTracks = [];
                LinkedListNode<Track>? node = _currentNode.Next;

                while (node is not null)
                {
                    nextTracks.Add(node.Value);
                    node = node.Next;
                }

                return nextTracks;
            }
        }
    }

    internal IReadOnlyList<Track> Previous
    {
        get
        {
            using (_lock.EnterScope())
            {
                if (_currentNode is null)
                {
                    return [];
                }
                
                List<Track> previousTracks = [];
                LinkedListNode<Track>? node = _currentNode.Previous;

                while (node is not null)
                {
                    previousTracks.Insert(0, node.Value);
                    node = node.Previous;
                }
                
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
                return _currentNode?.Next is not null;
            }
        }
    }

    internal bool HasPreviousTrack
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _currentNode?.Previous is not null;
            }
        }
    }

    internal Track? CurrentTrack
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _currentNode?.Value;
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

    private readonly LinkedList<Track> _tracks = [];
    private readonly Lock _lock = new();

    private LinkedListNode<Track>? _currentNode;

    internal TrackQueue(TrackQueueSnapshot? snapshot)
    {
        if (snapshot is null ||snapshot.Tracks.Count == 0)
        {
            return;
        }
        
        LoopMode = snapshot.LoopMode;
        int index = 0;
        
        foreach (Track track in snapshot.Tracks)
        {
            LinkedListNode<Track> node = _tracks.AddLast(track);

            if (index == snapshot.CurrentTrackIndex)
            {
                _currentNode = node;
            }

            index++;
        }
        
        // If index is ever out of bounds, default to the first item.
        _currentNode ??= _tracks.First;
    }
    
    internal void Add(Track track)
    {
        using (_lock.EnterScope())
        {
            _tracks.AddLast(track);
            _currentNode ??= _tracks.First;
        }
    }

    internal bool TryMoveToNext([NotNullWhen(true)] out Track? track)
    {
        using (_lock.EnterScope())
        {
            if (_currentNode is null)
            {
                track = null;
                return false;
            }

            if (LoopMode is LoopMode.Track)
            {
                track = _currentNode.Value;
                return true;
            }
            
            LinkedListNode<Track>? nextNode = _currentNode.Next;

            // Wrap around to the first item if at the end and queue looping.
            if (nextNode is null && LoopMode is LoopMode.Queue)
            {
                nextNode = _tracks.First;
            }

            if (nextNode is not null)
            {
                _currentNode = nextNode;
                track = nextNode.Value;
                
                return true;
            }

            // Set _currentNode to null so that the completed track can be pushed into Previous.
            _currentNode = _currentNode.Next;
            track = null;
            
            return false;
        }
    }

    internal bool TryMoveToPrevious([NotNullWhen(true)] out Track? track)
    {
        using (_lock.EnterScope())
        {
            LinkedListNode<Track>? targetNode = _currentNode is null ? _tracks.Last : _currentNode.Previous;
            
            if (targetNode is null)
            {
                track = null;
                return false;
            }
            
            _currentNode = targetNode;
            track = targetNode.Value;
            
            return true;
        }
    }

    internal bool Shuffle()
    {
        using (_lock.EnterScope())
        {
            LinkedListNode<Track>? startNode = _currentNode?.Next ?? _tracks.First;

            // Cannot shuffle with less than 2 items.
            if (startNode?.Next is null)
            {
                return false;
            }

            List<Track> remaining = [];
            LinkedListNode<Track>? current = startNode;

            while (current is not null)
            {
                remaining.Add(current.Value);
                current = current.Next;
            }
            
            // Fisher-Yates shuffle.
            for (int i = remaining.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (remaining[i], remaining[j]) = (remaining[j], remaining[i]);
            }
            
            // Remove old remaining nodes.
            while (startNode is not null)
            {
                LinkedListNode<Track>? next = startNode.Next;
                _tracks.Remove(startNode);
                
                startNode = next;
            }
            
            // Add the shuffled items back to the queue.
            foreach (Track track in remaining)
            {
                _tracks.AddLast(track);
            }

            return true;
        }
    }

    internal TrackQueueSnapshot GetSnapshot()
    {
        using (_lock.EnterScope())
        {
            LinkedListNode<Track>? node = _tracks.First;
            List<Track> snapshotTracks = [];
            int currentIndex = -1;

            // Capture max 100 items starting from the top.
            while (node is not null && snapshotTracks.Count < 100)
            {
                if (node == _currentNode)
                {
                    currentIndex = snapshotTracks.Count;
                }

                snapshotTracks.Add(node.Value);
                node = node.Next;
            }
            
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
            
            _tracks.Clear();
            _currentNode = null;
            
            return true;
        }
    }

    internal sealed record TrackQueueSnapshot(IReadOnlyList<Track> Tracks, int CurrentTrackIndex, LoopMode LoopMode);
}
