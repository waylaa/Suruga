using Suruga.FFmpeg.Primitives;
using Suruga.PostProcessing.Extensions;
using Suruga.PostProcessing.Primitives;
using Suruga.PostProcessing.Wsola.Analysis;
using Suruga.PostProcessing.Wsola.Buffers;
using Suruga.PostProcessing.Wsola.Matching;
using Suruga.PostProcessing.Wsola.Parameters;
using Suruga.PostProcessing.Wsola.Synthesis;

namespace Suruga.PostProcessing.Processors;

internal sealed class TimeStretch : IAudioProcessor
{
    internal float Tempo
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 1;
    
    private readonly WsolaBuffer _inputBuffer;
    private readonly int _channels;
    
    private readonly SignalAnalyzer _analyzer = new();
    private readonly WsolaParameterController _controller = new();
    private readonly WsolaWindow _window = new();
    private readonly OverlapAddSynthesizer _synthesizer;
    
    private float[] _previousFrame = [];

    private float _nominalInputPosition;
    private int _outputPosition;
    private int _lastSearchRadius;

    internal TimeStretch(int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        
        _channels = channels;
        _synthesizer = new OverlapAddSynthesizer();
        _inputBuffer = new WsolaBuffer(channels);
    }
    
    public AudioProcessorStatus SendFrame(AudioFramebuffer? frame)
    {
        if (frame is null || frame.IsEmpty || Tempo.IsApproximatelyEqualTo(1))
        {
            return AudioProcessorStatus.NoOp;
        }

        _inputBuffer.Append(frame.Samples);
        Process();
        
        return AudioProcessorStatus.Success;
    }

    public AudioProcessorStatus ReceiveFrame(out AudioFramebuffer? frame)
    {
        int availableFrames = Math.Min(_outputPosition, _synthesizer.LengthFrames);

        if (availableFrames <= 0)
        {
            frame = null;
            return AudioProcessorStatus.NeedMoreInput;
        }
        
        float[] samples = GC.AllocateUninitializedArray<float>(availableFrames * _channels);
        int framesRead = _synthesizer.Read(samples, _channels);
        
        _outputPosition -= framesRead;
        
        frame = new AudioFramebuffer(framesRead, _channels);
        samples.AsSpan(0, framesRead * _channels).CopyTo(frame.Samples);
        
        return AudioProcessorStatus.Success;
    }

    public void Reset()
    {
        _inputBuffer.Clear();
        _synthesizer.Clear();

        Array.Clear(_previousFrame);
        
        _nominalInputPosition = 0;
        _outputPosition = 0;
        _lastSearchRadius = 0;
    }

    private void Process()
    {
        while (true)
        {
            ReadOnlySpan<float> input = _inputBuffer.Samples;
            int inputFrames = input.Length / _channels;
            int nominalPosition = (int)Math.Round(_nominalInputPosition);

            if (nominalPosition < 0 || nominalPosition >= inputFrames)
            {
                break;
            }
            
            WsolaParameters parameters = _controller.Update(GetFeatures(input, nominalPosition));

            if (inputFrames - nominalPosition < parameters.WindowFrames)
            {
                break;
            }

            _lastSearchRadius = parameters.SearchRadiusFrames;
            
            ReadOnlySpan<float> nominalFrame = input.Slice(
                nominalPosition * _channels,
                parameters.WindowFrames * _channels);

            if (_previousFrame.Length == 0)
            {
                float[] firstFrame = nominalFrame.ToArray();
                _previousFrame = firstFrame;
                
                _synthesizer.Add
                (
                    firstFrame,
                    _window.Get(parameters.WindowFrames),
                    _outputPosition,
                    _channels
                );

                Advance(parameters);
                continue;
            }

            WsolaCandidate candidate = NccMatcher.FindBest
            (
                input,
                _previousFrame,
                nominalPosition,
                parameters.WindowFrames,
                parameters.OverlapFrames,
                parameters.SearchRadiusFrames,
                _channels
            );
            
            ReadOnlySpan<float> selectedFrame = input.Slice(
                candidate.InputPosition * _channels,
                parameters.WindowFrames * _channels);

            float[] currentFrame = selectedFrame.ToArray();
            
            _synthesizer.Add
            (
                currentFrame,
                _window.Get(parameters.WindowFrames),
                _outputPosition,
                _channels
            );

            _previousFrame = currentFrame;
            Advance(parameters);
        }
        
        int framesToDiscard = Math.Max(0, (int)MathF.Floor(_nominalInputPosition) - _lastSearchRadius);

        if (framesToDiscard > 0)
        {
            _inputBuffer.Discard(framesToDiscard);
            _nominalInputPosition -= framesToDiscard;
        }
    }

    private void Advance(WsolaParameters parameters)
    {
        int synthesisHop = parameters.SynthesisHopFrames;
        float analysisHop = synthesisHop * Tempo;
        
        _nominalInputPosition += analysisHop;
        _outputPosition += synthesisHop;
    }

    private SignalFeatures GetFeatures(ReadOnlySpan<float> input, int nominalPosition)
    {
        int availableFrames = input.Length / _channels - nominalPosition;
        int analysisFrames = Math.Min(availableFrames, 2048);
        
        ReadOnlySpan<float> analysis = input.Slice(
            nominalPosition * _channels,
            analysisFrames * _channels);

        return _analyzer.Analyze(analysis, _channels);
    }
}
