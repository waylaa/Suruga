namespace Suruga.Options;

/// <summary>
/// Represents configuration options for database persistence.
/// </summary>
internal sealed record DatabaseOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether database persistence is enabled.
    /// </summary>
    internal required bool Enable { get; set; }

    /// <summary>
    /// Gets or sets the file path of the SQLite database.
    /// Defaults to 'suruga.db' if not set.
    /// </summary>
    internal required string? Path { get; set; }
}
