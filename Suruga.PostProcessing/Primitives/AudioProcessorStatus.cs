namespace Suruga.PostProcessing.Primitives;

internal enum AudioProcessorStatus
{
    Success,
    NeedMoreInput,
    EndOfStream,
    NoOp
}
