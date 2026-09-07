namespace Suruga.PostProcessing.Wsola.Parameters;

internal sealed class ParameterSmoother
{
    private readonly int _minimumStep;
    private readonly float _stepFraction;

    internal ParameterSmoother(int minimumStep, float stepFraction = 0.125f)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumStep);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stepFraction);
        
        _minimumStep = minimumStep;
        _stepFraction = stepFraction;
    }

    internal int Advance(int current, int target)
    {
        int amount = Math.Max(_minimumStep, (int)(current * _stepFraction));

        if (current < target)
        {
            return Math.Min(current + amount, target);
        }

        if (current > target)
        {
            return Math.Max(current - amount, target);
        }

        return current;
    }
}
