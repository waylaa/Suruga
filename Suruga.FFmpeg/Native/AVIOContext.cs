using System.Runtime.InteropServices;

namespace Suruga.FFmpeg.Native;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct AVIOContext
{
    private nint av_class;
    private byte* buffer;
    private int buffer_size;
    private byte* buf_ptr;
    private byte* buf_end;
    private void* opaque;
    private delegate* unmanaged[Cdecl]<void*, byte*, int, int> read_packet;
    private delegate* unmanaged[Cdecl]<void*, byte*, int, int> write_packet;
    private delegate* unmanaged[Cdecl]<void*, long, int, long> seek;
    private long pos;
    private int eof_reached;
    private int error;
    private int write_flags;
    private int max_packet_size;
    private int min_packet_size;
    private ulong checksum;
    private byte* checksum_ptr;
    private delegate* unmanaged[Cdecl]<ulong, byte*, uint, ulong> update_checksum;
    private delegate* unmanaged[Cdecl]<void*, int, int> read_pause;
    private delegate* unmanaged[Cdecl]<void*, int, long, int, long> read_seek;
    private int seekable;
    private int direct;
    private byte* protocol_whitelist;
    private byte* protocol_blacklist;
    private delegate* unmanaged[Cdecl]<void*, byte*, int, int, long, int> write_data_type;
    private int ignore_boundary_point;
    private byte* buf_ptr_max;
    private long bytes_read;
    private long bytes_written;
}
