using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Suruga.Audio.Processors.Abstractions;

namespace Suruga.Audio.Processors;

/// <summary>
/// A polyphase sinc resampler.
/// </summary>
internal sealed class PitchProcessor2 : IAudioProcessor
{
    private readonly Lock _lock = new();
    private DcBlocker _dcBlockerL = new(0.995f);
    private DcBlocker _dcBlockerR = new(0.995f);
    private readonly Memory<float> _historyL = new float[HistorySize];
    private readonly Memory<float> _historyR = new float[HistorySize];
    private readonly Memory<float> _sincTable; // Flat table [PHASES * SINC_TAPS]
    private readonly float[] _simdBuffer = new float[Vector<float>.Count];

    private Memory<float> _leftBuffer = Memory<float>.Empty;
    private Memory<float> _rightBuffer = Memory<float>.Empty;
    private float _targetSpeed = 1f;
    private float _currentSpeed = 1f;
    private double _position = 0.0;
    private bool _positionValid = false;
    private float _smoothSpeed = 1f;

    private const float SmoothingTau = 0.05f;
    private const int HistorySize = 128;
    private const int SincTaps = 64;
    private const int Phases = 256;

    internal PitchProcessor2()
    {
        _sincTable = new float[Phases * SincTaps];
        ResetState();
        GenerateSincTable();
    }

    internal void SetPitch(float value)
    {
        using (_lock.EnterScope())
        {
            _targetSpeed = Math.Clamp(value, 0.1f, 2f);
        }
    }

    private void ResetState()
    {
        _position = 0;
        _positionValid = false;
        _smoothSpeed = 1f;
        _dcBlockerL.Reset();
        _dcBlockerR.Reset();
        _historyL.Span.Clear();
        _historyR.Span.Clear();
    }

    private void GenerateSincTable()
    {
        Span<float> tableSpan = _sincTable.Span;

        for (int phase = 0; phase < Phases; phase++)
        {
            float frac = phase / (float)Phases;

            for (int tap = 0; tap < SincTaps; tap++)
            {
                float x = tap - (SincTaps - 1) / 2f - frac;
                tableSpan[phase * SincTaps + tap] = WindowedSinc(x);
            }
        }
    }

    private static float WindowedSinc(float x)
    {
        const float pi = MathF.PI;

        if (MathF.Abs(x) < 1e-6f)
        {
            return 1f;
        }

        float sinc = MathF.Sin(pi * x) / (pi * x);
        float window = 0.5f + 0.5f * MathF.Cos(pi * x / (SincTaps - 1));

        return sinc * window;
    }

    public bool TryProcess(DecodedFrame<float> inputFrame, [NotNullWhen(true)] out DecodedFrame<float>? outputFrame)
    {
        // Smooth speed.
        float targetSpeed = Volatile.Read(ref _targetSpeed);
        _smoothSpeed += (_targetSpeed - _smoothSpeed) * (1 - MathF.Exp(-SmoothingTau));
        _currentSpeed = _smoothSpeed;

        float speed = _currentSpeed;

        int inputSamples = inputFrame.SamplePerChannelCount;

        if (inputSamples < 2)
        {
            outputFrame = null;
            return false;
        }

        DecodedFrame<float>.ReadOnlyDecodedFrameView inputView = inputFrame.GetReadOnlyView();
        ReadOnlySpan<float> inputSpan = inputView.Buffer;

        if (_leftBuffer.Length < inputSamples)
        {
            _leftBuffer = new float[inputSamples];
        }

        if (_rightBuffer.Length < inputSamples)
        {
            _rightBuffer = new float[inputSamples];
        }

        ExtractChannels(inputSpan, _leftBuffer.Span, _rightBuffer.Span, inputSamples);
        double availableInput = inputSamples - 1;

        if (_positionValid)
        {
            availableInput -= _position;
        }
        else
        {
            _positionValid = true;
        }

        int outputFrames = Math.Max(1, (int)Math.Floor(availableInput / speed));

        outputFrame = new DecodedFrame<float>(outputFrames * 2);
        using DecodedFrame<float>.PinnedDecodedFrame pinnedOutput = outputFrame.Pin();

        pinnedOutput.WithPinnedSpan((outputSpan) =>
        {
            double pos = _position;
            int outIdx = 0;
            ReadOnlySpan<float> leftSpan = _leftBuffer.Span.Slice(0, inputSamples);
            ReadOnlySpan<float> rightSpan = _rightBuffer.Span.Slice(0, inputSamples);

            int simdWidth = Vector<float>.Count;

            for (int i = 0; i < outputFrames; i++)
            {
                int idx = (int)Math.Floor(pos);
                float frac = (float)(pos - idx);

                float sampleL = PolyphaseSinc(leftSpan, _historyL.Span, idx, frac, simdWidth);
                float sampleR = PolyphaseSinc(rightSpan, _historyR.Span, idx, frac, simdWidth);

                sampleL = _dcBlockerL.Process(sampleL);
                sampleR = _dcBlockerR.Process(sampleR);

                // Soft clip.
                outputSpan[outIdx++] = MathF.Tanh(sampleL);
                outputSpan[outIdx++] = MathF.Tanh(sampleR);

                pos += speed;
            }

            double integerPart = Math.Floor(pos);
            _position = pos - integerPart;

            int copyCount = Math.Min(HistorySize, inputSamples);
            leftSpan.Slice(inputSamples - copyCount, copyCount).CopyTo(_historyL.Span.Slice(HistorySize - copyCount));
            rightSpan.Slice(inputSamples - copyCount, copyCount).CopyTo(_historyR.Span.Slice(HistorySize - copyCount));
        });

        return true;
    }

    private float PolyphaseSinc(ReadOnlySpan<float> x, Span<float> history, int idx, float frac, int simdWidth)
    {
        int phase = (int)(frac * Phases) % Phases;
        ReadOnlySpan<float> coeffs = _sincTable.Span.Slice(phase * SincTaps, SincTaps);

        float y = 0f;
        int i = 0;

        // SIMD loop with reusable buffer.
        for (; i <= SincTaps - simdWidth; i += simdWidth)
        {
            Vector<float> vCoeffs = new(coeffs.Slice(i, simdWidth));

            for (int j = 0; j < simdWidth; j++)
            {
                int xi = idx + i + j - SincTaps / 2;
                _simdBuffer[j] = xi < 0
                    ? history[HistorySize + xi]
                    : xi < x.Length ? x[xi] : x[x.Length - 1];
            }

            Vector<float> vSamples = new(_simdBuffer);
            y += Vector.Dot(vSamples, vCoeffs);
        }

        // Scalar remainder.
        for (; i < SincTaps; i++)
        {
            int xi = idx + i - SincTaps / 2;

            float sample = xi < 0
                ? history[HistorySize + xi]
                : xi < x.Length ? x[xi] : x[x.Length - 1];

            y += sample * coeffs[i];
        }

        return y;
    }

    private static void ExtractChannels(ReadOnlySpan<float> interleaved, Span<float> left, Span<float> right, int sampleCount)
    {
        for (int i = 0; i < sampleCount; i++)
        {
            left[i] = interleaved[i * 2];
            right[i] = interleaved[i * 2 + 1];
        }
    }

    private struct DcBlocker
    {
        private readonly float _coef;

        private float _prevInput;
        private float _prevOutput;

        internal DcBlocker(float coef)
            => _coef = coef;

        internal float Process(float input)
        {
            float y = input - _prevInput + _coef * _prevOutput;
            _prevInput = input;
            _prevOutput = y;

            return y;
        }

        internal void Reset()
            => _prevInput = _prevOutput = 0;
    }
}
