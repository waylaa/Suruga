using Suruga.PostProcessing.Wsola.Analysis;

namespace Suruga.PostProcessing.Wsola.Parameters;

internal sealed class WsolaParameterController
{
    private readonly int _minimumWindow;
    private readonly int _maximumWindow;
    private readonly int _searchRadius;

    private readonly ParameterSmoother _windowSmoother = new(64);
    private readonly ParameterSmoother _overlapSmoother = new(32);

    private WsolaParameters _current;
    
    internal WsolaParameterController() : this
    (
        1024,
        4096,
        512,
        new WsolaParameters(2048, 512, 512)
    )
    {
    }

    private WsolaParameterController
    (
        int minimumWindow,
        int maximumWindow,
        int searchRadius,
        WsolaParameters initial
    )
    {
        _minimumWindow = minimumWindow;
        _maximumWindow = maximumWindow;
        _searchRadius = searchRadius;
        
        initial.Validate();
        _current = initial;
    }
    
    internal WsolaParameters Update(SignalFeatures features)
    {
        float stability = Math.Clamp(features.Periodicity * (1 - features.Transientness), 0, 1);
        
        int rawWindow = Lerp(_minimumWindow, _maximumWindow, stability);
        int desiredWindow = rawWindow / 512 * 512; // Quantize target window (same thing as below). Lower values produce artifacts.
        int desiredOverlap = desiredWindow / 2; // Lock overlap to exact ratio (for example 50%).
        
        WsolaParameters target = new(desiredWindow, desiredOverlap, _searchRadius);

        // Snap smoothed values to 128-sample boundaries.
        int window = _windowSmoother.Advance(_current.WindowFrames, target.WindowFrames) / 256 * 256;
        int overlap = _overlapSmoother.Advance(_current.OverlapFrames, target.OverlapFrames) / 256 * 256;
        
        _current = new WsolaParameters(window, overlap, _searchRadius);
        _current.Validate();
        
        return _current;
    }

    private static int Lerp(int minimum, int maximum, float amount)
        => (int)MathF.Round(minimum + (maximum - minimum) * amount);
}
