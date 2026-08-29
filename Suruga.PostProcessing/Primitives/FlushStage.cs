namespace Suruga.PostProcessing.Primitives;

internal enum FlushStage
{
    Resampler,
    TimeStretchHops,
    TimeStretchTail,
    Done
}
