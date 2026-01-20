using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Suruga.Audio.Processors.Abstractions;

namespace Suruga.Audio.Processors;

internal sealed class VolumeProcessor : IAudioProcessor
{
    private readonly Lock _lock = new();
    private float _volume = 1;

    public bool TryProcess(DecodedFrame<float> inputFrame, [NotNullWhen(true)] out DecodedFrame<float>? outputFrame)
    {
        outputFrame = inputFrame;

        if (_volume == 1)
        {
            return true;
        }

        if (inputFrame.SampleCount == 0)
        {
            outputFrame = null;
            return false;
        }

        using DecodedFrame<float>.PinnedDecodedFrame inputPinnedFrame = inputFrame.Pin();

        float volume = Volatile.Read(in _volume);
        int vectorWidth = Vector<float>.Count;

        Vector<float> volumeVec = new(volume);
        int i = 0;

        inputPinnedFrame.WithPinnedSpan(inputBuffer =>
        {
            if (Vector.IsHardwareAccelerated)
            {
                for (; i <= inputBuffer.Length - vectorWidth; i += vectorWidth)
                {
                    Vector<float> sampleVec = new(inputBuffer.Slice(i, vectorWidth));
                    sampleVec *= volumeVec;

                    sampleVec.CopyTo(inputBuffer.Slice(i, vectorWidth));
                }

                // Scalar remainder loop.
                for (; i < inputBuffer.Length; i++)
                {
                    ref float sample = ref inputBuffer[i];
                    sample *= volume;
                }
            }
            else
            {
                for (; i < inputBuffer.Length; i++)
                {
                    ref float sample = ref inputBuffer[i];
                    sample *= volume;
                }
            }
        });

        return true;
    }

    internal void SetVolume(float value)
    {
        using (_lock.EnterScope())
        {
            value = Math.Clamp(value, 0f, 1f);
            Interlocked.Exchange(ref _volume, value);
        }
    }
}

