using Suruga.Audio.Primitives;
using Suruga.Primitives;

namespace Suruga.Audio.Events;

/// <summary>
/// Provides data for a player state change event.
/// </summary>
/// <param name="State">The new playback state.</param>
/// <param name="CurrentTrack">The currently active track, if it exists.</param>
/// <param name="HasErrored">
/// Indicates whether the state change was caused by an error.
/// </param>
internal sealed record PlayerStateChangedEventArgs(AudioPlaybackState State, Track? CurrentTrack, bool HasErrored);
