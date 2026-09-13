using System.Text.Json.Serialization;
using Suruga.Primitives;

namespace Suruga.Persistence;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(List<Track>))]
[JsonSerializable(typeof(Track))]
[JsonSerializable(typeof(TrackPlatform))]
[JsonSerializable(typeof(TrackRequestContext))]
internal sealed partial class TrackJsonContext : JsonSerializerContext;
