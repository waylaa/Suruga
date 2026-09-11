using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Suruga.FFmpeg.Interop;
using Suruga.FFmpeg.Native;

namespace Suruga.FFmpeg.Primitives;

public readonly unsafe ref struct DictionaryView(ref readonly AVDictionary pointer)
{
    private readonly ref readonly AVDictionary _pointer = ref pointer;
    
    public bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
    {
        if (Unsafe.IsNullRef(in _pointer))
        {
            value = null;
            return false;
        }
        
        Span<byte> utf8Key = Encoding.UTF8.GetBytes(key);
        
        AVDictionaryEntry* entry = NativeMethods.av_dict_get
        (
            in _pointer,
            in MemoryMarshal.GetReference(utf8Key),
            in Unsafe.NullRef<AVDictionaryEntry>(),
            Constants.AV_DICT_IGNORE_SUFFIX
        );

        if (entry is null || entry->value is null)
        {
            value = null;
            return false;
        }
            
        ReadOnlySpan<byte> utf8Value = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(entry->value);
        value = Encoding.UTF8.GetString(utf8Value);
        
        return true;
    }
}
