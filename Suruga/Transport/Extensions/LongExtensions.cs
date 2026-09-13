namespace Suruga.Transport.Extensions;

internal static class LongExtensions
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];
    
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
