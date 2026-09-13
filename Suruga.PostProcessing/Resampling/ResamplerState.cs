namespace Suruga.PostProcessing.Resampling;

internal sealed class ResamplerState(int channels)
{
    internal float[] PreviousFrame { get; } = GC.AllocateUninitializedArray<float>(channels);
    
    internal double Fraction;
    internal bool HasHistory;

    internal void Reset()
    {
        Fraction = 0;
        HasHistory = false;
        
        Array.Clear(PreviousFrame);
    }
}
