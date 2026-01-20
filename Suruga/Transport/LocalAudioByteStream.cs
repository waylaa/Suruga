using Suruga.Primitives;
using Suruga.Transport.Abstractions;
using Suruga.Transport.Primitives;

namespace Suruga.Transport;

internal sealed class LocalAudioByteStream(LocalAudioStreamDescriptor descriptor) : IAudioByteStream
{
    public long? Length => _fs.Length;

    private readonly FileStream _fs = new(descriptor.Url, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: false);

    public int Read(Span<byte> destination)
        => _fs.Read(destination);

    public long Seek(long offset, SeekOrigin origin)
        => _fs.Seek(offset, origin);

    public void Dispose()
        => _fs.Dispose();
}

