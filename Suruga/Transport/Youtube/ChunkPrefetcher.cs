namespace Suruga.Transport.Youtube;

internal sealed class ChunkPrefetcher
{
    internal Memory<byte> Buffer { get; private set; }

    internal long ChunkStart { get; private set; }
    
    internal long ChunkEnd { get; private set; }
    
    internal int ValidLength { get; private set; }
    
    private readonly int _chunkSize;

    private Memory<byte> _prefetchBuffer;
    
    private Task<int>? _prefetchTask;
    private long _prefetchChunkStart = -1;

    internal ChunkPrefetcher(int chunkSize)
    {
        _chunkSize = chunkSize;
        
        Buffer = GC.AllocateUninitializedArray<byte>(chunkSize);
        _prefetchBuffer = GC.AllocateUninitializedArray<byte>(chunkSize);
    }

    internal void LoadChunk(Func<long, long, Memory<byte>, int> fetch, long position, long streamLength)
    {
        long chunkStart = position;
        long chunkEnd = Math.Min(position + _chunkSize - 1, streamLength - 1);

        bool isPrefetchUsed = false;

        // The background task already fetched this chunk.
        if (_prefetchTask is not null && _prefetchChunkStart == chunkStart)
        {
            try
            {
                int fetched = _prefetchTask.GetAwaiter().GetResult();
                _prefetchTask = null;

                (Buffer, _prefetchBuffer) = (_prefetchBuffer, Buffer);

                ValidLength = fetched;
                isPrefetchUsed = true;
            }
            catch
            {
                _prefetchTask = null;
                _prefetchChunkStart = -1;
            }
        }

        if (!isPrefetchUsed)
        {
            Invalidate();
            ValidLength = fetch(chunkStart, chunkEnd, Buffer);
        }
        
        ChunkStart = chunkStart;
        ChunkEnd = chunkEnd;
        
        // Start the next chunk in the background.
        long nextStart = chunkEnd + 1;

        if (nextStart < streamLength)
        {
            long nextEnd = Math.Min(nextStart + _chunkSize - 1, streamLength - 1);
            _prefetchChunkStart = nextStart;

            Memory<byte> bufferToFill = _prefetchBuffer;
            _prefetchTask = Task.Run(() => fetch(nextStart, nextEnd, bufferToFill));
        }
    }

    internal bool IsOutsidePrefetchWindow(long position)
        => position < _prefetchChunkStart || position >= _prefetchChunkStart + _chunkSize;

    internal void InvalidateActiveChunk()
        => ValidLength = 0;

    internal void Invalidate()
    {
        if (_prefetchTask is not null)
        {
            try
            {
                _prefetchTask.Wait();
            }
            catch
            {
                // Ignore.
            }
            
            _prefetchTask = null;
        }

        _prefetchChunkStart = -1;
    }
}
