using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SoundTouch;
using Suruga.Audio.Decode.Primitives;
using Suruga.Extensions;

namespace Suruga.Audio;

/// <summary>
/// Applies audio effects to decoded PCM audio frames.
/// </summary>
internal sealed class AudioPostProcessor
{
	private readonly SoundTouchProcessor _processor;

	private float _gain = 1f;
	private float _tempo = 1f;
	private float _pitch = 1f;
	private float _rate = 1f;
	private bool _hasDecoderSignaledEos;
	private bool _hasFlushed;
	
	private const int Channels = 2;

	/// <summary>
	/// Initializes a new audio post-processor.
	/// </summary>
	internal AudioPostProcessor()
	{
		_processor = new SoundTouchProcessor
		{
			SampleRate = 48000,
			Channels = Channels,
			Tempo = 1f,
			Pitch = 1f,
			Rate = 1f
		};
		
		_processor.SetSetting(SettingId.UseAntiAliasFilter, 1);
		_processor.SetSetting(SettingId.UseQuickSeek, 1);
	}
	
	/// <summary>
	/// Applies the configured audio effects to a decoded frame.
	/// </summary>
	/// <param name="input">The input frame to process.</param>
	/// <param name="output">
	/// When this method returns <see langword="true"/>, contains the processed frame.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if a processed frame is available; otherwise,
	/// <see langword="false"/>.
	/// </returns>
	internal bool TryPostProcessFrame(IAudioFrameBuffer input, [NotNullWhen(true)] out IAudioFrameBuffer? output)
	{
		// No-op.
		if (IsNeutral())
		{
			output = input;
			return true;
		}

		Span<float> samples = input.Samples;
		float gain = _gain;

		if (!gain.IsApproximatelyOne())
		{
			ApplyGain(samples, gain);
		}

		// Push into SoundTouch (frames per channel).
		int frames = input.SampleCount / Channels;
		_processor.PutSamples(samples, frames);

		input.Dispose();
		return TryDrainToFrame(out output);
	}
	
	/// <summary>
	/// Flushes any buffered audio remaining in the post processor.
	/// </summary>
	/// <param name="outputFrame">
	/// When this method returns <see langword="true"/>, contains the flushed frame.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if a frame was produced; otherwise, <see langword="false"/>.
	/// </returns>
	internal bool TryFlush([NotNullWhen(true)] out IAudioFrameBuffer? outputFrame)
	{
		if (!_hasDecoderSignaledEos)
		{
			outputFrame = null;
			return false;
		}

		if (!_hasFlushed)
		{
			_processor.Flush();
			_hasFlushed = true;
		}
		
		return TryDrainToFrame(out outputFrame);
	}

	/// <summary>
	/// Sets the output gain multiplier.
	/// </summary>
	/// <param name="gain">The gain factor to apply.</param>
	internal void SetGain(float gain)
		=> _gain = gain;

	/// <summary>
	/// Sets the playback tempo without affecting pitch.
	/// </summary>
	/// <param name="tempo">The tempo multiplier.</param>
	internal void SetTempo(float tempo)
	{
		_rate = 1f;
		_processor.Tempo = _tempo = tempo;
	}

	/// <summary>
	/// Sets the playback pitch without affecting tempo.
	/// </summary>
	/// <param name="pitch">The pitch multiplier.</param>
	internal void SetPitch(float pitch)
	{
		_rate = 1f;
		_processor.Pitch = _pitch = pitch;
	}
	
	/// <summary>
	/// Sets the playback rate, affecting both tempo and pitch.
	/// </summary>
	/// <param name="rate">The rate multiplier.</param>
	internal void SetRate(float rate)
		=> _processor.Rate = _rate = rate;
	
	/// <summary>
	/// Signals that no additional decoded audio frames will be provided.
	/// </summary>
	internal void SignalEndOfStream()
		=> _hasDecoderSignaledEos = true;
	
	/// <summary>
	/// Clears all buffered state and restores the processor to its initial state.
	/// </summary>
	internal void Reset()
	{
		if (!_processor.IsEmpty)
		{
			_processor.Clear();
		}
			
		_hasDecoderSignaledEos = false;
		_hasFlushed = false;
	}

	private bool TryDrainToFrame([NotNullWhen(true)] out IAudioFrameBuffer? outputFrame)
	{
		int availableSamples = _processor.AvailableSamples;

		if (availableSamples <= 0)
		{
			outputFrame = null;
			return false;
		}

		outputFrame = new ManagedAudioFrameBuffer(availableSamples * Channels);
		_processor.ReceiveSamples(outputFrame.Samples, availableSamples);
		
		return true;
	}
	
	private static void ApplyGain(Span<float> samples, float gain)
	{
		if (Vector.IsHardwareAccelerated)
		{
			Vector<float> gainVector = new(gain);
			Span<Vector<float>> vectors = MemoryMarshal.Cast<float, Vector<float>>(samples);
			
			for (int i = 0; i < vectors.Length; i++)
			{
				vectors[i] *= gainVector;
			}

			// Scalar tail.
			int tail = vectors.Length * Vector<float>.Count;
		
			for (int i = tail; i < samples.Length; i++)
			{
				samples[i] *= gain;
			}
		}
		else
		{
			for (int i = 0; i < samples.Length; i++)
			{
				samples[i] *= gain;
			}
		}
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool IsNeutral()
		=> _gain.IsApproximatelyOne() && _tempo.IsApproximatelyOne() && _pitch.IsApproximatelyOne() && _rate.IsApproximatelyOne();
}
