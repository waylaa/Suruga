namespace Suruga.PostProcessing.Wsola.Parameters;

internal sealed record WsolaParameters(int WindowFrames, int OverlapFrames, int SearchRadiusFrames)
{
    internal int SynthesisHopFrames => WindowFrames - OverlapFrames;

    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(WindowFrames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(OverlapFrames);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(OverlapFrames, WindowFrames);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(SearchRadiusFrames);
    }
}
