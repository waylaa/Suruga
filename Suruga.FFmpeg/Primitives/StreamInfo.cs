using Suruga.FFmpeg.Helpers;
using Suruga.FFmpeg.Native;

namespace Suruga.FFmpeg.Primitives;

public readonly unsafe ref struct StreamInfo(MediaStream stream, AVCodec* pCodec)
{
    public readonly MediaStream Stream = stream;
    public readonly ref readonly AVCodec Codec = ref PointerHelper.GetReadOnlyReference(pCodec);
}
