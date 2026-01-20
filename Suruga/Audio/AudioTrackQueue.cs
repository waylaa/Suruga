using System.Runtime.InteropServices;
using Suruga.Primitives;

namespace Suruga.Audio;

internal sealed class AudioTrackQueue
{
    private readonly List<AudioTrack> _queue = [];
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _itemsAvailable = new(initialCount: 0, maxCount: int.MaxValue);

    internal void Enqueue(AudioTrack track)
    {
        using (_lock.EnterScope())
        {
            _queue.Add(track);
        }
        _itemsAvailable.Release();
    }

    internal AudioTrack? Dequeue()
    {
        using (_lock.EnterScope())
        {
            if (_queue.Count == 0)
            {
                return null;
            }

            AudioTrack track = _queue[0];
            _queue.RemoveAt(0);
            return track;
        }
    }

    internal async ValueTask<bool> WaitForItemAsync(CancellationToken token = default)
    {
        try
        {
            await _itemsAvailable.WaitAsync(token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    internal IReadOnlyList<AudioTrack> PeekAll()
    {
        using (_lock.EnterScope())
        {
            return _queue.ToArray();
        }
    }

    internal void Clear()
    {
        int itemsToRemove;

        using (_lock.EnterScope())
        {
            itemsToRemove = _queue.Count;
            _queue.Clear();
        }

        // Drain semaphore
        for (int i = 0; i < itemsToRemove; i++)
        {
            _itemsAvailable.Wait(0);
        }
    }

    internal void Shuffle()
    {
        using (_lock.EnterScope())
        {
            if (_queue.Count <= 1)
            {
                return;
            }

            Random.Shared.Shuffle(CollectionsMarshal.AsSpan(_queue));
        }
    }
}