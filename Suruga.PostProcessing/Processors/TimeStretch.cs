using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
    
    private readonly ILogger<TimeStretch> _logger;
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

    internal TimeStretch(int channels, ILoggerFactory loggerFactory)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        
        _channels = channels;
        _logger = loggerFactory.CreateLogger<TimeStretch>();
        
        _synthesizer = new OverlapAddSynthesizer(loggerFactory.CreateLogger<OverlapAddSynthesizer>());
        _inputBuffer = new WsolaBuffer(channels);
    }
    
    public AudioProcessorStatus SendFrame(AudioFrameBuffer? frame)
    {
        if (frame is null || frame.IsEmpty || Tempo.IsApproximatelyEqualTo(1))
        {
            return AudioProcessorStatus.NoOp;
        }
        
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            ReadOnlySpan<float> samples = frame.Samples;
            float peak = 0;
            float sumSqaures = 0;
            
            foreach (float sample in frame.Samples)
            {
                float abs = MathF.Abs(sample);
                
                if (abs > peak)
                {
                    peak = abs;
                }
                sumSqaures += sample * sample;
            }
            float rms = samples.Length == 0 ? 0 : MathF.Sqrt(sumSqaures / samples.Length);
            
            _logger.LogTrace
            (
                $"[TS] INPUT: samples={samples.Length} " +
                $"frames={samples.Length / _channels} " +
                $"peak={peak:E6} " +
                $"rms={rms:E6}"
            );
        }

        _inputBuffer.Append(frame.Samples);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace
            (
                $"[TS] SendFrame: bufferLen={_inputBuffer.LengthFrames} " +
                $"nominalPos={_nominalInputPosition} " +
                $"outputPos={_outputPosition} " +
                $"synthLen={_synthesizer.LengthFrames}"
            );
        }
        
        Process();
        return AudioProcessorStatus.Success;
    }

    public AudioProcessorStatus ReceiveFrame(out AudioFrameBuffer? frame)
    {
        int availableFrames = Math.Min(_outputPosition, _synthesizer.LengthFrames);

        if (availableFrames <= 0)
        {
            frame = null;
            return AudioProcessorStatus.NeedMoreInput;
        }
        
        float[] samples = GC.AllocateUninitializedArray<float>(availableFrames * _channels);
        int framesRead = _synthesizer.Read(samples, _channels);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            ReadOnlySpan<float> output = samples.AsSpan(0, framesRead * _channels);
            
            float peak = 0;
            float energy = 0;
            
            foreach (float sample in output)
            {
                float abs = MathF.Abs(sample);
                
                if (abs > peak)
                {
                    peak = abs;
                }
                
                energy += sample * sample;
            }
            
            _logger.LogTrace($"[TS] ReceiveFrame: frames={framesRead} peak={peak} rms={Math.Sqrt(energy / output.Length)}");
        }
        
        _outputPosition -= framesRead;
        
        frame = new AudioFrameBuffer(framesRead, _channels);
        samples.AsSpan(0, framesRead * _channels).CopyTo(frame.Samples);
        
        return AudioProcessorStatus.Success;
    }
    
    private void Process()
    {
        int iterations = 0;
        Stopwatch? stopwatch = null;
        
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            stopwatch = Stopwatch.StartNew();
        }
        
        while (true)
        {
            iterations++;
            
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
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace
                    (
                        $"[TS] Stall: inputFrames={inputFrames}" +
                        $"nominalPos={nominalPosition}" +
                        $"needed={parameters.WindowFrames}" +
                        $"overlap={parameters.OverlapFrames}" +
                        $"search={parameters.SearchRadiusFrames}"
                    );
                }
                
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

            WsolaCandidate candidate;

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                Stopwatch st = Stopwatch.StartNew();
            
                candidate = NccMatcher.FindBest
                (
                    input,
                    _previousFrame,
                    nominalPosition,
                    parameters.WindowFrames,
                    parameters.OverlapFrames,
                    parameters.SearchRadiusFrames,
                    _channels
                );
            
                st.Stop();
                _logger.LogTrace($"[NCC] {stopwatch?.Elapsed.TotalMilliseconds:F2} ms");
            }
            else
            {
                candidate = NccMatcher.FindBest
                (
                    input,
                    _previousFrame,
                    nominalPosition,
                    parameters.WindowFrames,
                    parameters.OverlapFrames,
                    parameters.SearchRadiusFrames,
                    _channels
                );
            }
            
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

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            stopwatch?.Stop();
            
            _logger.LogTrace
            (
                $"[TS] Process: {stopwatch?.ElapsedMilliseconds} ms, " +
                $"iterations={iterations}, " +
                $"inputFrames={_inputBuffer.LengthFrames}, " +
                $"nominal={_nominalInputPosition}"
            );
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

    public void Dispose()
    {
    }
}
