namespace Suruga.Options;

/// <summary>
/// Represents configuration options for connecting to an Invidious Companion instance.
/// </summary>
internal sealed record InvidiousCompanionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether Invidious Companion integration is enabled.
    /// </summary>
    internal required bool Enable { get; set; }

    /// <summary>
    /// Gets or sets the secret key used to authenticate requests to the Invidious Companion instance.
    /// </summary>
    internal required string SecretKey { get; set; }

    /// <summary>
    /// Gets or sets the hostname or IP address of the Invidious Companion instance.
    /// </summary>
    internal required string Host { get; set; }

    /// <summary>
    /// Gets or sets the port used to connect to the Invidious Companion instance.
    /// </summary>
    internal required int Port { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether HTTPS should be used when connecting to the Invidious Companion instance.
    /// </summary>
    internal required bool UseHttps { get; set; }
}
