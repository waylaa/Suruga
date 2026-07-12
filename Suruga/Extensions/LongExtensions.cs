namespace Suruga.Extensions;

/// <summary>
/// Provides extension methods for <see cref="long"/> values.
/// </summary>
internal static class LongExtensions
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// Converts a byte count to a human-readable string with appropriate units (B, KB, MB).
    /// </summary>
    /// <param name="bytes">The number of bytes to format.</param>
    /// <returns></returns>
    internal static string ToFormattedBytes(this long bytes)
    {
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:F2} {Units[unit]}";
    }
}
