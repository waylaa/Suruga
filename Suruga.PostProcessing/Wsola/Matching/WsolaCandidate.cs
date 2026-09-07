namespace Suruga.PostProcessing.Wsola.Matching;

internal readonly struct WsolaCandidate
{
    internal int InputPosition { get; }

    internal WsolaCandidate(int inputPosition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputPosition);
        InputPosition = inputPosition;
    }
}
