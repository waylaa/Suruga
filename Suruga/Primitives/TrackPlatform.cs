using System.Text.Json.Serialization;

namespace Suruga.Primitives;

[JsonConverter(typeof(JsonStringEnumConverter<TrackPlatform>))]
internal enum TrackPlatform
{
    Local,
    Youtube
}
