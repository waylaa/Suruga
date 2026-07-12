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
    /// Gets or sets the connection string used to connect to the database server.
    /// </summary>
    internal required string ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the name of the database.
    /// </summary>
    internal required string Name { get; set; }
}
