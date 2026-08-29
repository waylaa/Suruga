using System.Runtime.CompilerServices;

namespace Suruga.Audio.Extensions;

/// <summary>
/// Provides extension methods for <see cref="float"/> values.
/// </summary>
internal static class FloatExtensions
{
	/// <summary>
	/// Determines whether <paramref name="value"/> is approximately equal to
	/// 1.0 within a small tolerance.
	/// </summary>
	/// <param name="value">The float value to check.</param>
	/// <returns>
	/// <c>true</c> if the absolute difference between <paramref name="value"/>
	/// and 1.0 is less than 0.001, otherwise <c>false</c>.
	/// </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool IsApproximatelyOne(this float value)
		=> Math.Abs(value - 1f) < 0.001f;
}
