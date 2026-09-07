using Suruga.PostProcessing.Wsola.Converters;

namespace Suruga.PostProcessing.Wsola.Analysis;

internal sealed class SignalAnalyzer
{
    private readonly TransientAnalyzer _transient = new();
    private readonly PeriodicityAnalyzer _periodicity;
    
    // Minimum periodicity is the shortest period it will detect the highest pitch
    // and maximum periodicity for the lowest pitch. This range covers most musical
    // instruments and speech. 48kHz sample rate is Discord's requirement.
    private const int MinimumPeriodicity = 32; // 48kHz / 32 = 1500Hz
    private const int MaximumPeriodicity = 800; // 48kHz / 800 = 60Hz

    internal SignalAnalyzer()
        => _periodicity = new PeriodicityAnalyzer(MinimumPeriodicity, MaximumPeriodicity);

    internal SignalFeatures Analyze(ReadOnlySpan<float> samples, int channels)
    {
        float[] mono = MonoConverter.Convert(samples, channels);
        
        float energy = EnergyAnalyzer.Analyze(mono);
        float transientness = _transient.Analyze(energy);
        float periodicity = _periodicity.Analyze(mono);
        
        return new SignalFeatures(energy, periodicity, transientness);
    }
}
