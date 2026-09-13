namespace Suruga.Options;

internal sealed record InvidiousCompanionOptions
{
    internal required bool Enable { get; set; }
    
    internal required string SecretKey { get; set; }
    
    internal required string Host { get; set; }
    
    internal required int Port { get; set; }
    
    internal required bool UseHttps { get; set; }
}
