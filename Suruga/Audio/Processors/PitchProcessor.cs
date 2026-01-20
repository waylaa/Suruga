using System.Diagnostics.CodeAnalysis;
using Suruga.Audio.Processors.Abstractions;

namespace Suruga.Audio.Processors;

/// <summary>
/// Linear speed resampler.
/// </summary>
internal sealed class PitchProcessor : IAudioProcessor
{
    private readonly Lock _lock = new();
    private float _speed = 1f;

    // Read position in input frames (fractional).
    private double _position;

    // Carry-over for frame boundaries (stereo).
    private float _lastSampleLeftChannel;
    private float _lastSampleRightChannel;
    private bool _hasLastSample;

    public bool TryProcess(DecodedFrame<float> inputFrame, [NotNullWhen(true)] out DecodedFrame<float>? outputFrame)
    {
        float speed = Volatile.Read(in _speed);

        if (speed == 1)
        {
            outputFrame = inputFrame;
            return true;
        }

        const int channels = 2;
        int inputSamples = inputFrame.SamplePerChannelCount;

        if (inputSamples < 2)
        {
            outputFrame = null;
            return false;
        }

        DecodedFrame<float>.ReadOnlyDecodedFrameView inputView = inputFrame.GetReadOnlyView();

        // How many output frames we can safely generate.
        int outputFrames = (int)Math.Floor((inputSamples - 1 - _position) / speed);

        if (outputFrames <= 0)
        {
            outputFrame = null;
            return false;
        }

        outputFrame = new DecodedFrame<float>(outputFrames * channels);
        using DecodedFrame<float>.PinnedDecodedFrame pinnedOutputFrame = outputFrame.Pin();

        pinnedOutputFrame.WithPinnedSpan(inputView, (dest, src) =>
        {
            double pos = _position;
            int outIndex = 0;

            for (int i = 0; i < outputFrames; i++)
            {
                int baseFrame = (int)pos;
                double frac = pos - baseFrame;

                float l0, r0, l1, r1;

                if (baseFrame == 0 && _hasLastSample)
                {
                    // Interpolate from previous frame tail.
                    l0 = _lastSampleLeftChannel;
                    r0 = _lastSampleRightChannel;
                    l1 = src[0];
                    r1 = src[1];
                }
                else
                {
                    // Clamp to valid range.
                    baseFrame = Math.Min(baseFrame, inputSamples - 2);

                    int baseIndex = baseFrame * channels;
                    int nextIndex = baseIndex + channels;

                    l0 = src[baseIndex];
                    r0 = src[baseIndex + 1];
                    l1 = src[nextIndex];
                    r1 = src[nextIndex + 1];
                }

                dest[outIndex++] = (float)(l0 + (l1 - l0) * frac);
                dest[outIndex++] = (float)(r0 + (r1 - r0) * frac);

                pos += speed;
            }

            // Consume whole input frames, keep fractional remainder.
            int consumedFrames = (int)pos;
            _position = pos - consumedFrames;

            // Store tail sample for next call.
            int tailIndex = (inputSamples - 1) * channels;
            _lastSampleLeftChannel = src[tailIndex];
            _lastSampleRightChannel = src[tailIndex + 1];
            _hasLastSample = true;
        });

        return true;
    }

    internal void SetSpeed(float value)
    {
        using (_lock.EnterScope())
        {
            Interlocked.Exchange(ref _speed, Math.Clamp(value, 0.25f, 2f));
        }
    }
}
