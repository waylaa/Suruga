namespace Suruga.PostProcessing.Wsola.Analysis;

internal sealed class TransientAnalyzer
{
    private float _previousEnergy;

    internal float Analyze(float energy)
    {
        if (_previousEnergy <= 0)
        {
            _previousEnergy = energy;
            return 0;
        }

        float difference = MathF.Abs(energy - _previousEnergy);
        float scale = MathF.Max(_previousEnergy, 1e-6f);

        _previousEnergy = energy;
        return Math.Clamp(difference / scale, 0, 1);
    }
}
