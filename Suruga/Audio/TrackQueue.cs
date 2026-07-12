using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Suruga.Audio.Primitives;
using Suruga.Persistence;
using Suruga.Primitives;

namespace Suruga.Audio;

internal sealed class TrackQueue
{
	internal IReadOnlyList<Track> History => _history.AsReadOnly();
	
	internal IReadOnlyList<Track> Upcoming => _upcoming.AsReadOnly();

	internal bool HasCurrent => CurrentTrack is not null;

	internal bool HasPrevious => _history.Count > 0;
	
	private bool IsTrackLooping => LoopMode is LoopMode.PerTrack;

	private bool IsQueueLooping => LoopMode is LoopMode.PerQueue;

	internal bool HasNext
	{
		get
		{
			if (IsTrackLooping)
			{
				return CurrentTrack is not null;
			}

			if (_upcoming.Count > 0)
			{
				return true;
			}

			return IsQueueLooping && _history.Count > 0;
		}
	}
	
	internal Track? CurrentTrack { get; private set; }

	internal LoopMode LoopMode { get; set; }

	private readonly List<Track> _history = [];
	private readonly List<Track> _upcoming = [];

	internal TrackQueue(TrackQueueState? storedState)
	{
		if (storedState is null)
		{
			return;
		}
		
		_history.AddRange(storedState.History);
		_upcoming.AddRange(storedState.Upcoming);
		
		CurrentTrack = storedState.CurrentTrack;
		LoopMode = storedState.LoopMode;

        // Prevent duplicate current track after restoration.
        if (CurrentTrack is not null)
        {
            int index = _upcoming.FindIndex(track => TracksMatch(track, CurrentTrack));

			if (index >= 0)
			{
				_upcoming.RemoveAt(index);
			}
        }
    }
	
	internal void EnqueueRange(IEnumerable<Track> tracks)
		=> _upcoming.AddRange(tracks);

	internal bool TryDequeue([NotNullWhen(true)] out Track? track)
	{
		track = null;

		if (CurrentTrack is not null || _upcoming.Count == 0)
		{
			return false;
		}

		track = CurrentTrack = PopUpcomingTrack();
		return true;
	}

	internal bool TrySkip([NotNullWhen(true)] out Track? nextTrack)
	{
		nextTrack = null;

		if (CurrentTrack is null)
		{
			return false;
		}
		
		_history.Add(CurrentTrack);

		if (_upcoming.Count == 0 && IsQueueLooping)
		{
			_upcoming.AddRange(_history);
			_history.Clear();
		}

		if (_upcoming.Count == 0)
		{
			CurrentTrack = null;
			return false;
		}

		nextTrack = CurrentTrack = PopUpcomingTrack();
		return true;
	}

	internal bool TryMoveNext([NotNullWhen(true)] out Track? nextTrack)
	{
		nextTrack = null;

		if (CurrentTrack is null)
		{
			return false;
		}

		if (IsTrackLooping)
		{
			nextTrack = CurrentTrack;
			return true;
		}
		
		_history.Add(CurrentTrack);

		if (_upcoming.Count == 0 && IsQueueLooping)
		{
			_upcoming.AddRange(_history);
			_history.Clear();
		}

		if (_upcoming.Count == 0)
		{
			CurrentTrack = null;
			return false;
		}
		
		nextTrack = CurrentTrack = PopUpcomingTrack();
		return true;
	}
	
	internal bool TryMovePrevious([NotNullWhen(true)] out Track? previousTrack)
	{
		previousTrack = null;

		if (_history.Count == 0)
		{
			return false;
		}

		if (CurrentTrack is not null)
		{
			_upcoming.Insert(0, CurrentTrack);
		}

		int index = _history.Count - 1;

		previousTrack = CurrentTrack = _history[index];
		_history.RemoveAt(index);

		return true;
	}

	internal void CycleLoopMode()
	{
		LoopMode = LoopMode switch
		{
			LoopMode.None => LoopMode.PerTrack,
			LoopMode.PerTrack => LoopMode.PerQueue,
			_ => LoopMode.None
		};
	}

	internal void Shuffle()
	{
		if (_upcoming.Count <= 1)
		{
			return;
		}
		
		Random.Shared.Shuffle(CollectionsMarshal.AsSpan(_upcoming));
	}

	/// <summary>
	/// Moves <see cref="CurrentTrack"/> into <see cref="_history"/> so a new track can be dequeued.
	/// </summary>
	internal void ArchiveCurrentTrack()
	{
		if (CurrentTrack is null)
		{
			return;
		}

		_history.Add(CurrentTrack);
		CurrentTrack = null;
	}

	internal void Clear()
	{
		_history.Clear();
		_upcoming.Clear();
		
		CurrentTrack = null;
		LoopMode = LoopMode.None;
	}

	private Track PopUpcomingTrack()
	{
		Track track = _upcoming[0];
		_upcoming.RemoveAt(0);
		
		return track;
	}

	private static bool TracksMatch(Track? a, Track? b)
		=> a?.Platform == b?.Platform && a?.Id == b?.Id && a?.Uri == b?.Uri;
}
