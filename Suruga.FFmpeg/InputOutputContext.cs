using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Suruga.FFmpeg.Helpers;
using Suruga.FFmpeg.Interop;
using Suruga.FFmpeg.Native;

namespace Suruga.FFmpeg;

public sealed unsafe class InputOutputContext : FFmpegResource<AVIOContext>
{
    private readonly Stream _byteStream;
    
    private GCHandle _selfHandle;
    
    private const int BufferSize = 32 * 1024; // 32KB
    
    public InputOutputContext(Stream byteStream)
    {
        _byteStream = byteStream;
        
        byte* pBuffer = (byte*)NativeMethods.av_malloc(BufferSize);

        if (pBuffer is null)
        {
            throw new OutOfMemoryException("Failed to allocate buffer for AVIOContext.");
        }

        _selfHandle = GCHandle.Alloc(this);

        Pointer = NativeMethods.avio_alloc_context
        (
            ref PointerHelper.GetReference(pBuffer),
            BufferSize,
            0,
            GCHandle.ToIntPtr(_selfHandle).ToPointer(),
            &ReadPacket,
            null,
            &Seek
        );
        
        if (!IsInitialized)
        {
            NativeMethods.av_freep(pBuffer);
            _selfHandle.Free();
            
            throw new OutOfMemoryException("Failed to allocate an AVIOContext.");
        }
    }
    
    protected override void Release(ref AVIOContext* addressOfPointer)
    {
        NativeMethods.avio_context_free(ref addressOfPointer);

        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ReadPacket(void* opaque, byte* buf, int bufSize)
    {
        try
        {
            if (GCHandle.FromIntPtr((nint)opaque).Target is not InputOutputContext { IsInitialized: true } context)
            {
                return Constants.AVERROR_EIO;
            }

            Span<byte> destination = new(buf, bufSize);
            int bytesRead = context._byteStream.Read(destination);

            return bytesRead switch
            {
                > 0 => bytesRead,
                0 => Constants.AVERROR_EOF,
                _ => Constants.AVERROR_EIO
            };
        }
        catch
        {
            return Constants.AVERROR_EIO;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static long Seek(void* opaque, long offset, int whence)
    {
        try
        {
            if (GCHandle.FromIntPtr((nint)opaque).Target is not InputOutputContext { IsInitialized: true } context)
            {
                return Constants.AVERROR_EIO;
            }

            // Size query.
            if ((whence & Constants.AVSEEK_SIZE) != 0)
            {
                return context._byteStream.Length;
            }

            int actualWhence = whence & ~Constants.AVSEEK_SIZE;

            SeekOrigin origin = actualWhence switch
            {
                Constants.AVSEEK_SET => SeekOrigin.Begin,
                Constants.AVSEEK_CUR => SeekOrigin.Current,
                Constants.AVSEEK_END => SeekOrigin.End,
                _ => (SeekOrigin)(-1)
            };

            if ((int)origin == -1)
            {
                return -1;
            }

            long newPosition = context._byteStream.Seek(offset, origin);

            if (newPosition < 0)
            {
                return -1;
            }

            return newPosition;
        }
        catch
        {
            return -1;
        }
    }
}
