namespace Suruga.Options;

internal sealed record DatabaseOptions
{
    internal required bool Enable { get; set; }
    
    // Defaults to 'suruga.db' if null.
    internal string? Path { get; set; }
}
