namespace Suruga.PostProcessing.Resampling;

internal sealed class ResamplerState
{
    internal float[] PreviousFrame { get; }
    
    internal double Fraction;
    internal bool HasHistory;
    
    internal ResamplerState(int channels)
        => PreviousFrame = GC.AllocateUninitializedArray<float>(channels);

    internal void Reset()
    {
        Fraction = 0;
        HasHistory = false;
        
        Array.Clear(PreviousFrame);
    }
}
