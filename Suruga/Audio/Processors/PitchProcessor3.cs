using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Suruga.Audio.Processors.Abstractions;

namespace Suruga.Audio.Processors;

/// <summary>
/// High-quality windowed sinc resampler with 128-tap Kaiser window.
/// Optimized for quality, -96dB aliasing rejection, flat to 22kHz.
/// </summary>
internal sealed class PitchProcessor3 : IAudioProcessor, IDisposable
{
    private readonly int _inputSampleRate;
    private readonly int _outputSampleRate;
    private readonly int _channels;
    private readonly double _ratio;
    private readonly Memory<float> _sincTable;
    private readonly Memory<float> _inputRing;
    private readonly Memory<float> _tempOutput;

    private int _ringWritePos;
    private double _outputTime;
    private bool _isInitialized;

    private const int SincTaps = 128;
    private const int Phases = 8192;
    private const double KaiserBeta = 9.6;
    private const int MaxChannels = 8;

    internal PitchProcessor3(int inputSampleRate, int outputSampleRate, int channels = 2)
    {
        if (channels is < 1 or > MaxChannels)
        {
            throw new ArgumentException($"Channels must be between 1 and {MaxChannels}", nameof(channels));
        }

        _inputSampleRate = inputSampleRate;
        _outputSampleRate = outputSampleRate;
        _channels = channels;
        _ratio = (double)outputSampleRate / inputSampleRate;

        int ringSize = (SincTaps + 1024) * _channels;
        _inputRing = new float[ringSize];
        _tempOutput = new float[8192 * _channels];
        _sincTable = new float[Phases * SincTaps];

        GenerateKaiserWindowedSincTable();
        Reset();

        _isInitialized = true;
    }

    public bool TryProcess(DecodedFrame<float> inputFrame, [NotNullWhen(true)] out DecodedFrame<float>? outputFrame)
    {
        if (!_isInitialized)
        {
            outputFrame = null;
            return false;
        }

        DecodedFrame<float>.ReadOnlyDecodedFrameView inputView = inputFrame.GetReadOnlyView();
        ReadOnlySpan<float> input = inputView.Buffer;

        int inputFrames = inputFrame.SamplePerChannelCount;
        int estimatedOutputFrames = (int)Math.Ceiling(inputFrames * _ratio) + 2;
        int estimatedOutputSamples = estimatedOutputFrames * _channels;

        Span<float> outputSpan = _tempOutput.Span.Slice(0, Math.Min(estimatedOutputSamples, _tempOutput.Length));

        bool success = ProcessInternal(input, outputSpan, out int samplesRead, out int samplesWritten);

        if (!success || samplesWritten == 0)
        {
            outputFrame = null;
            return false;
        }

        int totalSamples = samplesWritten * _channels;
        outputFrame = new DecodedFrame<float>(totalSamples);

        using DecodedFrame<float>.PinnedDecodedFrame pinnedOutput = outputFrame.Pin();
        pinnedOutput.WithPinnedSpan(outputSpan, (outputBuffer, outputSpan) =>
        {
            outputSpan.Slice(0, totalSamples).CopyTo(outputBuffer);
        });

        return true;
    }

    internal void Reset()
    {
        _inputRing.Span.Clear();
        _ringWritePos = SincTaps / 2 * _channels;
        _outputTime = 0;
    }

    private bool ProcessInternal
    (
        ReadOnlySpan<float> input,
        Span<float> output,
        out int samplesRead,
        out int samplesWritten
    )
    {
        int inputFrames = input.Length / _channels;
        int outputFrames = output.Length / _channels;

        samplesRead = 0;
        samplesWritten = 0;

        Span<float> ring = _inputRing.Span;
        int ringSize = ring.Length;

        while (samplesWritten < outputFrames && samplesRead < inputFrames)
        {
            double nextOutputTime = _outputTime + 1.0;
            int requiredInputFrames = (int)Math.Ceiling(nextOutputTime / _ratio);

            while (samplesRead < inputFrames && _ringWritePos < ringSize - _channels)
            {
                for (int ch = 0; ch < _channels; ch++)
                {
                    ring[_ringWritePos++] = input[(samplesRead * _channels) + ch];
                }

                samplesRead++;
            }

            int requiredSamples = SincTaps * _channels;

            if (_ringWritePos < requiredSamples)
            {
                break;
            }

            double inputPos = _outputTime / _ratio;
            int inputIndex = (int)Math.Floor(inputPos);
            double frac = inputPos - inputIndex;
            int phase = (int)(frac * Phases) & (Phases - 1);
            int readOffset = inputIndex * _channels;

            if (readOffset + requiredSamples > _ringWritePos)
            {
                break;
            }

            for (int ch = 0; ch < _channels; ch++)
            {
                float sample = InterpolateChannel(ring, readOffset + ch, phase);
                output[(samplesWritten * _channels) + ch] = sample;
            }

            samplesWritten++;
            _outputTime += 1.0;

            if (inputIndex > 0 && _ringWritePos > SincTaps * _channels)
            {
                int shiftFrames = inputIndex;
                int shiftSamples = shiftFrames * _channels;

                ring.Slice(shiftSamples).CopyTo(ring);

                _ringWritePos -= shiftSamples;
                _outputTime -= shiftFrames * _ratio;
            }
        }

        return samplesWritten > 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float InterpolateChannel(ReadOnlySpan<float> ring, int offset, int phase)
    {
        ReadOnlySpan<float> coeffs = _sincTable.Span.Slice(phase * SincTaps, SincTaps);

        float sum = 0f;
        int stride = _channels;

        if (Vector.IsHardwareAccelerated && SincTaps >= Vector<float>.Count)
        {
            int simdCount = Vector<float>.Count;
            Vector<float> vSum = Vector<float>.Zero;
            Span<float> tempSamples = stackalloc float[simdCount];

            int i = 0;

            for (; i <= SincTaps - simdCount; i += simdCount)
            {
                Vector<float> vCoeffs = new(coeffs.Slice(i, simdCount));

                for (int j = 0; j < simdCount; j++)
                {
                    tempSamples[j] = ring[offset + (i + j) * stride];
                }

                Vector<float> vSamples = new(tempSamples);
                vSum += vSamples * vCoeffs;
            }

            for (int j = 0; j < simdCount; j++)
            {
                sum += vSum[j];
            }

            for (; i < SincTaps; i++)
            {
                sum += ring[offset + i * stride] * coeffs[i];
            }
        }
        else
        {
            for (int i = 0; i < SincTaps; i++)
            {
                sum += ring[offset + i * stride] * coeffs[i];
            }
        }

        return sum;
    }

    private void GenerateKaiserWindowedSincTable()
    {
        Span<float> table = _sincTable.Span;
        double cutoff = 0.95;

        for (int phase = 0; phase < Phases; phase++)
        {
            double phaseFrac = (double)phase / Phases;

            for (int tap = 0; tap < SincTaps; tap++)
            {
                double x = tap - (SincTaps - 1) / 2.0 - phaseFrac;
                double sinc = ComputeSinc(x * cutoff);
                double window = ComputeKaiserWindow(tap, SincTaps, KaiserBeta);

                table[phase * SincTaps + tap] = (float)(sinc * window);
            }

            NormalizePhase(table.Slice(phase * SincTaps, SincTaps));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeSinc(double x)
    {
        if (Math.Abs(x) < 1e-9)
        {
            return 1.0;
        }

        double pix = Math.PI * x;
        return Math.Sin(pix) / pix;
    }

    private static double ComputeKaiserWindow(int n, int length, double beta)
    {
        double alpha = (length - 1) / 2.0;
        double x = (n - alpha) / alpha;

        return BesselI0(beta * Math.Sqrt(1.0 - x * x)) / BesselI0(beta);
    }

    private static double BesselI0(double x)
    {
        double sum = 1.0;
        double term = 1.0;
        double x2 = x * x / 4.0;

        for (int i = 1; i < 50; i++)
        {
            term *= x2 / (i * i);
            sum += term;

            if (term < 1e-12 * sum)
            {
                break;
            }
        }

        return sum;
    }

    private static void NormalizePhase(Span<float> phase)
    {
        double sum = 0;

        for (int i = 0; i < phase.Length; i++)
        {
            sum += phase[i];
        }

        if (Math.Abs(sum) > 1e-9)
        {
            float scale = (float)(1.0 / sum);

            for (int i = 0; i < phase.Length; i++)
            {
                phase[i] *= scale;
            }
        }
    }

    public void Dispose()
        => _isInitialized = false;
}