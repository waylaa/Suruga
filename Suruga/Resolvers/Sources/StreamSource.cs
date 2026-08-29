namespace Suruga.Resolvers.Sources;

/// <summary>
/// Serves as the abstract base record for all stream source implementations.
/// </summary>
/// <remarks>
/// Derived records are expected to provide the necessary parameters and metadata 
/// required to instantiate specific stream types (e.g., local files, remote URLs).
/// </remarks>
internal abstract record StreamSource;
