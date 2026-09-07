using System.Runtime.CompilerServices;

namespace Suruga.PostProcessing.Resampling;

internal static class CatmullRomInterpolator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Interpolate(float p0, float p1, float p2, float p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;
        
        return (float)(0.5f * 
        (
            2 * p1 +
            (-p0 + p2) * t +
            (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
            (-p0 + 3 * p1 - 3 * p2 + p3) * t3
        ));
    }
}
