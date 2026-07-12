namespace Suruga.Audio.Primitives;

/// <summary>
/// Represents the playback state of an audio player.
/// </summary>
internal enum AudioPlaybackState
{
	/// <summary>
	/// The player is idle and not actively processing audio.
	/// </summary>
	Idle,

	/// <summary>
	/// Audio is currently playing.
	/// </summary>
	Playing,

	/// <summary>
	/// Playback is paused and can be resumed.
	/// </summary>
	Paused
}
