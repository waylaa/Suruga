using Suruga.Primitives;

namespace Suruga.Audio.Primitives;

internal sealed record TrackFinishedEventArgs(Track Track, Exception? Error = null);
