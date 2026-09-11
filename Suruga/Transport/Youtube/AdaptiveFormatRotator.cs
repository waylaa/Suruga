using Suruga.Resolvers.Primitives;

namespace Suruga.Transport.Youtube;

/// <summary>
/// Tracks which adaptive format is currently active and advances to the next
/// lower-priority format when the current one is exhausted.
/// </summary>
internal sealed class AdaptiveFormatRotator(IReadOnlyList<AdaptiveFormat> formats)
{
    internal bool IsLast => _index >= formats.Count - 1;
    
    internal AdaptiveFormat Current { get; private set; } = formats[0];

    private int _index;

    internal AdaptiveFormat RotateToNext()
    {
        _index++;
        Current = formats[_index];
        
        return Current;
    }
}
