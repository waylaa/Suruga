using System.ComponentModel;
using NetCord.Services.ApplicationCommands;

namespace Suruga.Audio.Primitives;

/// <summary>
/// Specifies how playback looping is applied.
/// </summary>
public enum LoopMode
{
	/// <summary>
	/// Looping is disabled.
	/// </summary>
	None,

	/// <summary>
	/// Repeats the currently playing track.
	/// </summary>
	[SlashCommandChoice(Name = "Per-track")]
	[Description("per-track")]
	PerTrack,

	/// <summary>
	/// Repeats the entire track queue.
	/// </summary>
	[SlashCommandChoice(Name = "Per-queue")]
	[Description("per-queue")]
	PerQueue
}
