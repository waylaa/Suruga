namespace Suruga.PostProcessing.Wsola.Synthesis;

internal sealed class WsolaWindow
{
    private float[] _window = GC.AllocateUninitializedArray<float>(0);

    internal ReadOnlySpan<float> Get(int length)
    {
        EnsureCapacity(length);
        return _window;
    }

    private void EnsureCapacity(int length)
    {
        if (_window.Length == length)
        {
            return;
        }
        
        Array.Resize(ref _window, length);

        for (int i = 0; i < length; i++)
        {
            _window[i] = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * i / (length - 1));
        }
    }
}
