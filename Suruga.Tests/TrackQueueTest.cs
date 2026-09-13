using Suruga.Audio;
using Suruga.Audio.Primitives;
using Suruga.Primitives;

namespace Suruga.Tests;

[TestFixture]
public sealed class TrackQueueTests
{
    private static Track CreateTrack(string id)
        => new(TrackPlatform.Local, id, $"local://{id}") { Title = id };

    private static List<Track> CreateTracks(int count)
        => Enumerable.Range(0, count)
            .Select(i => CreateTrack($"T{i}"))
            .ToList();

    private static TrackQueue.TrackQueueSnapshot Snapshot(
        IReadOnlyList<Track> tracks,
        int currentIndex = 0,
        LoopMode loopMode = LoopMode.None)
        => new(tracks, currentIndex, loopMode);

    #region Construction

    [Test]
    public void Constructor_NullSnapshot_CreatesEmptyQueue()
    {
        TrackQueue queue = new(null);

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.Null);
            Assert.That(queue.Next, Is.Empty);
            Assert.That(queue.Previous, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.False);
            Assert.That(queue.HasPreviousTrack, Is.False);
        });
    }

    [Test]
    public void Constructor_EmptySnapshot_CreatesEmptyQueue()
    {
        TrackQueue queue = new(Snapshot([]));

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.Null);
            Assert.That(queue.Next, Is.Empty);
            Assert.That(queue.Previous, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.False);
            Assert.That(queue.HasPreviousTrack, Is.False);
        });
    }

    [Test]
    public void Constructor_ValidSnapshot_RestoresPositionAndLoopMode()
    {
        List<Track> tracks = CreateTracks(3);

        TrackQueue queue = new(
            Snapshot(tracks, currentIndex: 1, loopMode: LoopMode.Queue));

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[1]));
            Assert.That(queue.LoopMode, Is.EqualTo(LoopMode.Queue));
            Assert.That(queue.Previous, Is.EqualTo(new[] { tracks[0] }));
            Assert.That(queue.Next, Is.EqualTo(new[] { tracks[2] }));
            Assert.That(queue.HasPreviousTrack, Is.True);
            Assert.That(queue.HasNextTrack, Is.True);
        });
    }

    [Test]
    public void Constructor_OutOfRangeIndex_DefaultsToFirstTrack()
    {
        List<Track> tracks = CreateTracks(3);

        TrackQueue queue = new(Snapshot(tracks, currentIndex: 99));

        Assert.That(queue.CurrentTrack, Is.SameAs(tracks[0]));
    }

    [Test]
    public void Constructor_NegativeIndex_DefaultsToFirstTrack()
    {
        List<Track> tracks = CreateTracks(2);

        TrackQueue queue = new(Snapshot(tracks, currentIndex: -1));

        Assert.That(queue.CurrentTrack, Is.SameAs(tracks[0]));
    }

    #endregion

    #region Add

    [Test]
    public void Add_ToEmptyQueue_BecomesCurrentTrack()
    {
        TrackQueue queue = new(null);
        Track track = CreateTrack("A");

        queue.Add(track);

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.SameAs(track));
            Assert.That(queue.Next, Is.Empty);
            Assert.That(queue.Previous, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.False);
            Assert.That(queue.HasPreviousTrack, Is.False);
        });
    }

    [Test]
    public void Add_WhileTracksRemainQueued_AppendsToNext()
    {
        TrackQueue queue = new(null);
        Track first = CreateTrack("A");
        Track second = CreateTrack("B");

        queue.Add(first);
        queue.Add(second);

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.SameAs(first));
            Assert.That(queue.Next, Is.EqualTo(new[] { second }));
            Assert.That(queue.Previous, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.True);
        });
    }

    [Test]
    public void Add_AfterExhaustion_NewTrackBecomesCurrent_NotTheFinishedTrack()
    {
        TrackQueue queue = new(null);

        Track a = CreateTrack("A");
        Track b = CreateTrack("B");

        queue.Add(a);
        queue.Add(b);

        queue.TryMoveToNext(out _); // B

        bool advanced = queue.TryMoveToNext(out Track? afterExhaustion);

        Assert.Multiple(() =>
        {
            Assert.That(advanced, Is.False);
            Assert.That(afterExhaustion, Is.Null);
            Assert.That(queue.CurrentTrack, Is.Null);
            Assert.That(queue.Previous, Is.EqualTo(new[] { a, b }));
        });

        Track c = CreateTrack("C");

        queue.Add(c);

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.SameAs(c));
            Assert.That(queue.Previous, Is.EqualTo(new[] { a, b }));
            Assert.That(queue.Next, Is.Empty);
            Assert.That(queue.HasPreviousTrack, Is.True);
            Assert.That(queue.HasNextTrack, Is.False);
        });
    }

    [Test]
    public void Add_TwiceAfterExhaustion_OnlyFirstTrackJumpsTheQueue()
    {
        TrackQueue queue = new(null);

        Track a = CreateTrack("A");
        Track c = CreateTrack("C");
        Track d = CreateTrack("D");

        queue.Add(a);
        queue.TryMoveToNext(out _); // exhausted

        queue.Add(c);
        queue.Add(d);

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.SameAs(c));
            Assert.That(queue.Previous, Is.EqualTo(new[] { a }));
            Assert.That(queue.Next, Is.EqualTo(new[] { d }));
        });
    }

    [Test]
    public void Add_AfterRewinding_AppendsAfterExistingForwardTracks()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(3);

        tracks.ForEach(queue.Add);

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // T2
        queue.TryMoveToPrevious(out _); // T1

        Track t3 = CreateTrack("T3");

        queue.Add(t3);

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[1]));
            Assert.That(queue.Previous, Is.EqualTo(new[] { tracks[0] }));
            Assert.That(queue.Next, Is.EqualTo(new[] { tracks[2], t3 }));
        });
    }

    [Test]
    public void Add_AfterRewinding_DoesNotChangeCurrentTrack()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(3);

        tracks.ForEach(queue.Add);

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // T2
        queue.TryMoveToPrevious(out _); // T1

        queue.Add(CreateTrack("T3"));

        Assert.That(queue.CurrentTrack, Is.SameAs(tracks[1]));
    }

    #endregion

    #region TryMoveToNext

    [Test]
    public void TryMoveToNext_EmptyQueue_ReturnsFalse()
    {
        TrackQueue queue = new(null);

        bool moved = queue.TryMoveToNext(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.False);
            Assert.That(track, Is.Null);
        });
    }

    [Test]
    public void TryMoveToNext_AdvancesThroughQueueInOrder()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(3);

        tracks.ForEach(queue.Add);

        queue.TryMoveToNext(out Track? second);
        queue.TryMoveToNext(out Track? third);

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(tracks[1]));
            Assert.That(third, Is.SameAs(tracks[2]));
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[2]));
        });
    }

    [Test]
    public void TryMoveToNext_AtEndWithoutLoop_SetsPastEndAndKeepsHistory()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);

        queue.TryMoveToNext(out _); // T1

        bool moved = queue.TryMoveToNext(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.False);
            Assert.That(track, Is.Null);
            Assert.That(queue.CurrentTrack, Is.Null);
            Assert.That(queue.Previous, Is.EqualTo(tracks));
            Assert.That(queue.Next, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.False);
            Assert.That(queue.HasPreviousTrack, Is.True);
        });
    }

    [Test]
    public void TryMoveToNext_TrackLoop_RepeatsCurrentTrackIndefinitely()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.TryMoveToNext(out _); // T1

        queue.LoopMode = LoopMode.Track;

        Assert.That(queue.HasNextTrack, Is.True);

        for (int i = 0; i < 3; i++)
        {
            bool moved = queue.TryMoveToNext(out Track? track);

            Assert.Multiple(() =>
            {
                Assert.That(moved, Is.True);
                Assert.That(track, Is.SameAs(tracks[1]));
                Assert.That(queue.CurrentTrack, Is.SameAs(tracks[1]));
            });
        }
    }

    [Test]
    public void TryMoveToNext_TrackLoop_DoesNotCreateHistory()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.TryMoveToNext(out _); // T1

        queue.LoopMode = LoopMode.Track;

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        Assert.That(queue.Previous, Is.EqualTo(new[] { tracks[0] }));
    }

    [Test]
    public void TryMoveToNext_QueueLoop_WrapsToStartAndArchivesLapAsHistory()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(3);

        tracks.ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // T2

        bool wrapped = queue.TryMoveToNext(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(wrapped, Is.True);
            Assert.That(track, Is.SameAs(tracks[0]));
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[0]));
            Assert.That(queue.Previous, Is.EqualTo(tracks));
            Assert.That(queue.Next, Is.EqualTo(new[] { tracks[1], tracks[2] }));
            Assert.That(queue.HasNextTrack, Is.True);
            Assert.That(queue.HasPreviousTrack, Is.True);
        });
    }

    [Test]
    public void TryMoveToNext_QueueLoop_MultipleWraps_AccumulatesHistory()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        // T0 -> T1 -> T0 -> T1 -> T0
        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[0]));
            Assert.That(
                queue.Previous,
                Is.EqualTo(new[] { tracks[0], tracks[1], tracks[0], tracks[1] }));
        });
    }

    [Test]
    public void TryMoveToNext_QueueLoop_HistoryIsCappedForLongRunningLoops()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        for (int i = 0; i < 120; i++)
        {
            queue.TryMoveToNext(out _);
        }

        Assert.That(queue.Previous, Has.Count.LessThanOrEqualTo(100));
    }

    [Test]
    public void TryMoveToNext_AfterRewinding_ReplaysForwardTrack()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(3);

        tracks.ForEach(queue.Add);

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // T2
        queue.TryMoveToPrevious(out _); // T1

        bool moved = queue.TryMoveToNext(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.True);
            Assert.That(track, Is.SameAs(tracks[2]));
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[2]));
        });
    }

    #endregion

    #region TryMoveToPrevious

    [Test]
    public void TryMoveToPrevious_EmptyQueue_ReturnsFalse()
    {
        TrackQueue queue = new(null);

        bool moved = queue.TryMoveToPrevious(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.False);
            Assert.That(track, Is.Null);
        });
    }

    [Test]
    public void TryMoveToPrevious_MovesBackwardThroughQueue()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(3);

        tracks.ForEach(queue.Add);

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // T2

        bool moved = queue.TryMoveToPrevious(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.True);
            Assert.That(track, Is.SameAs(tracks[1]));
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[1]));
            Assert.That(queue.Next, Is.EqualTo(new[] { tracks[2] }));
            Assert.That(queue.Previous, Is.EqualTo(new[] { tracks[0] }));
        });
    }

    [Test]
    public void TryMoveToPrevious_AtStartWithNoHistory_ReturnsFalse()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        bool moved = queue.TryMoveToPrevious(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.False);
            Assert.That(track, Is.Null);
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[0]));
            Assert.That(queue.Previous, Is.Empty);
            Assert.That(queue.Next, Is.EqualTo(new[] { tracks[1] }));
            Assert.That(queue.HasPreviousTrack, Is.False);
            Assert.That(queue.HasNextTrack, Is.True);
        });
    }

    [Test]
    public void TryMoveToPrevious_WhilePastEnd_ReturnsLastPlayedTrackAndClearsPastEnd()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // exhausted

        bool moved = queue.TryMoveToPrevious(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.True);
            Assert.That(track, Is.SameAs(tracks[1]));
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[1]));
            Assert.That(queue.HasNextTrack, Is.False);
            Assert.That(queue.HasPreviousTrack, Is.True);
        });
    }

    [Test]
    public void TryMoveToPrevious_AcrossLoopBoundary_RestoresLastLapTrackFromHistory()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // wrap -> T0

        bool moved = queue.TryMoveToPrevious(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.True);
            Assert.That(track, Is.SameAs(tracks[1]));
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[1]));
            Assert.That(queue.Previous, Is.EqualTo(new[] { tracks[0] }));
            Assert.That
            (
                queue.Next,
                Is.EqualTo(new[] { tracks[0] }),
                "forward navigation should replay exactly as before rewinding"
            );
        });
    }

    [Test]
    public void TryMoveToPrevious_AcrossLoopBoundaryThenNext_RestoresSameForwardPath()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // T0

        queue.TryMoveToPrevious(out Track? previous);
        queue.TryMoveToNext(out Track? next);

        Assert.Multiple(() =>
        {
            Assert.That(previous, Is.SameAs(tracks[1]));
            Assert.That(next, Is.SameAs(tracks[0]));
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[0]));
        });
    }

    [Test]
    public void TryMoveToPrevious_MultipleStepsThenForward_ReplaysExactPath()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(4);

        tracks.ForEach(queue.Add);

        // T0 -> T1 -> T2 -> T3
        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        // T3 -> T2 -> T1
        queue.TryMoveToPrevious(out Track? previous1);
        queue.TryMoveToPrevious(out Track? previous2);

        // T1 -> T2 -> T3
        queue.TryMoveToNext(out Track? next1);
        queue.TryMoveToNext(out Track? next2);

        Assert.Multiple(() =>
        {
            Assert.That(previous1, Is.SameAs(tracks[2]));
            Assert.That(previous2, Is.SameAs(tracks[1]));
            Assert.That(next1, Is.SameAs(tracks[2]));
            Assert.That(next2, Is.SameAs(tracks[3]));
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[3]));
        });
    }

    [Test]
    public void TryMoveToPrevious_RewindEntireLapThenForward_RestoresExactSequence()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(3);

        tracks.ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        // T0 -> T1 -> T2 -> T0
        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        // T0 -> T2 -> T1 -> T0
        queue.TryMoveToPrevious(out Track? p1);
        queue.TryMoveToPrevious(out Track? p2);
        queue.TryMoveToPrevious(out Track? p3);

        // Forward should replay T1 -> T2 -> T0.
        queue.TryMoveToNext(out Track? n1);
        queue.TryMoveToNext(out Track? n2);
        queue.TryMoveToNext(out Track? n3);

        Assert.Multiple(() =>
        {
            Assert.That(p1, Is.SameAs(tracks[2]));
            Assert.That(p2, Is.SameAs(tracks[1]));
            Assert.That(p3, Is.SameAs(tracks[0]));

            Assert.That(n1, Is.SameAs(tracks[1]));
            Assert.That(n2, Is.SameAs(tracks[2]));
            Assert.That(n3, Is.SameAs(tracks[0]));
        });
    }

    [Test]
    public void TryMoveToPrevious_AcrossMultipleLoopBoundaries_RestoresHistoryInOrder()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        // T0 -> T1 -> T0 -> T1 -> T0
        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        // Walk backwards:
        // T0 -> T1 -> T0 -> T1
        queue.TryMoveToPrevious(out Track? p1);
        queue.TryMoveToPrevious(out Track? p2);
        queue.TryMoveToPrevious(out Track? p3);

        Assert.Multiple(() =>
        {
            Assert.That(p1, Is.SameAs(tracks[1]));
            Assert.That(p2, Is.SameAs(tracks[0]));
            Assert.That(p3, Is.SameAs(tracks[1]));
        });
    }

    [Test]
    public void TryMoveToPrevious_AfterLoopBoundary_DoesNotDuplicateTrack()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // T0

        queue.TryMoveToPrevious(out _); // T1

        Assert.Multiple(() =>
        {
            Assert.That(queue.Previous, Is.EqualTo(new[] { tracks[0] }));
            Assert.That(queue.Next, Is.EqualTo(new[] { tracks[0] }));
            Assert.That(queue.Next, Has.Count.EqualTo(1));
        });
    }

    #endregion

    #region HasNextTrack / HasPreviousTrack

    [Test]
    public void HasNextTrack_False_WhenQueueEmpty()
    {
        TrackQueue queue = new(null);
        Assert.That(queue.HasNextTrack, Is.False);
    }

    [Test]
    public void HasNextTrack_True_WhenMoreTracksQueued()
    {
        TrackQueue queue = new(null);
        CreateTracks(2).ForEach(queue.Add);

        Assert.That(queue.HasNextTrack, Is.True);
    }

    [Test]
    public void HasNextTrack_False_OnLastTrackWithoutLoop()
    {
        TrackQueue queue = new(null);
        CreateTracks(1).ForEach(queue.Add);

        Assert.That(queue.HasNextTrack, Is.False);
    }

    [TestCase(LoopMode.Track)]
    [TestCase(LoopMode.Queue)]
    public void HasNextTrack_True_OnLastTrackWithLoopEnabled(LoopMode loopMode)
    {
        TrackQueue queue = new(null);

        CreateTracks(1).ForEach(queue.Add);
        queue.LoopMode = loopMode;

        Assert.That(queue.HasNextTrack, Is.True);
    }

    [Test]
    public void HasNextTrack_False_WhenPastEnd()
    {
        TrackQueue queue = new(null);
        CreateTracks(2).ForEach(queue.Add);
        
        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        Assert.That(queue.HasNextTrack, Is.False);
    }

    [Test]
    public void HasPreviousTrack_False_OnFirstTrackWithNoHistory()
    {
        TrackQueue queue = new(null);

        CreateTracks(2).ForEach(queue.Add);

        Assert.That(queue.HasPreviousTrack, Is.False);
    }

    [Test]
    public void HasPreviousTrack_True_AfterAdvancing()
    {
        TrackQueue queue = new(null);

        CreateTracks(2).ForEach(queue.Add);
        queue.TryMoveToNext(out _);

        Assert.That(queue.HasPreviousTrack, Is.True);
    }

    [Test]
    public void HasPreviousTrack_True_WhenPastEnd()
    {
        TrackQueue queue = new(null);

        CreateTracks(2).ForEach(queue.Add);

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        Assert.That(queue.HasPreviousTrack, Is.True);
    }

    [Test]
    public void HasPreviousTrack_True_AfterCrossingLoopBoundary()
    {
        TrackQueue queue = new(null);

        CreateTracks(2).ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        Assert.That(queue.HasPreviousTrack, Is.True);
    }

    #endregion

    #region LoopMode

    [Test]
    public void LoopMode_DefaultsToNone()
    {
        TrackQueue queue = new(null);
        Assert.That(queue.LoopMode, Is.EqualTo(LoopMode.None));
    }

    [TestCase(LoopMode.None)]
    [TestCase(LoopMode.Track)]
    [TestCase(LoopMode.Queue)]
    public void LoopMode_CanBeChanged(LoopMode loopMode)
    {
        TrackQueue queue = new(null) { LoopMode = loopMode };
        Assert.That(queue.LoopMode, Is.EqualTo(loopMode));
    }

    [Test]
    public void ChangingLoopMode_FromNoneToQueue_AtLastTrack_AllowsWrapping()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.TryMoveToNext(out _); // T1

        queue.LoopMode = LoopMode.Queue;
        bool moved = queue.TryMoveToNext(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.True);
            Assert.That(track, Is.SameAs(tracks[0]));
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[0]));
        });
    }

    [Test]
    public void ChangingLoopMode_FromQueueToNone_AtLastTrack_StopsAtEnd()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        queue.TryMoveToNext(out _); // T1

        queue.LoopMode = LoopMode.None;

        bool moved = queue.TryMoveToNext(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.False);
            Assert.That(track, Is.Null);
            Assert.That(queue.CurrentTrack, Is.Null);
            Assert.That(queue.Previous, Is.EqualTo(tracks));
        });
    }

    [Test]
    public void ChangingLoopMode_FromTrackToNone_StopsRepeating()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.TryMoveToNext(out _); // T1

        queue.LoopMode = LoopMode.Track;

        queue.TryMoveToNext(out _); // T1 again

        queue.LoopMode = LoopMode.None;

        bool moved = queue.TryMoveToNext(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.False);
            Assert.That(track, Is.Null);
            Assert.That(queue.CurrentTrack, Is.Null);
        });
    }

    #endregion

    #region Shuffle

    [Test]
    public void Shuffle_WithFewerThanTwoUpcomingTracks_ReturnsFalse()
    {
        TrackQueue queue = new(null);
        CreateTracks(1).ForEach(queue.Add);

        Assert.That(queue.Shuffle(), Is.False);
    }

    [Test]
    public void Shuffle_WithNoUpcomingTracks_ReturnsFalse()
    {
        TrackQueue queue = new(null);
        queue.Add(CreateTrack("A"));

        Assert.That(queue.Shuffle(), Is.False);
    }

    [Test]
    public void Shuffle_WithTwoUpcomingTracks_ReturnsTrue()
    {
        TrackQueue queue = new(null);
        CreateTracks(3).ForEach(queue.Add);

        Assert.That(queue.Shuffle(), Is.True);
    }

    [Test]
    public void Shuffle_LeavesCurrentAndPreviousTracksUntouched()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(5);

        tracks.ForEach(queue.Add);
        queue.TryMoveToNext(out _); // T1

        queue.Shuffle();

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[1]));
            Assert.That(queue.Previous, Is.EqualTo(new[] { tracks[0] }));
            Assert.That(queue.Next, Is.EquivalentTo([tracks[2], tracks[3], tracks[4]]));
        });
    }

    [Test]
    public void Shuffle_PreservesExactlyTheSameUpcomingTracks()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(6);

        tracks.ForEach(queue.Add);
        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // T2

        Track[] expectedUpcoming =
        [
            tracks[3],
            tracks[4],
            tracks[5]
        ];

        bool shuffled = queue.Shuffle();

        Assert.Multiple(() =>
        {
            Assert.That(shuffled, Is.True);
            Assert.That(queue.Next, Is.EquivalentTo(expectedUpcoming));
            Assert.That(queue.Next, Has.Count.EqualTo(expectedUpcoming.Length));
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[2]));
        });
    }

    [Test]
    public void Shuffle_AfterRewinding_OnlyShufflesTracksAfterCurrent()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(5);

        tracks.ForEach(queue.Add);

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // T2
        queue.TryMoveToNext(out _); // T3
        queue.TryMoveToPrevious(out _); // T2

        bool shuffled = queue.Shuffle();

        Assert.Multiple(() =>
        {
            Assert.That(shuffled, Is.True);
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[2]));
            Assert.That(queue.Previous, Is.EqualTo(new[] { tracks[0], tracks[1] }));
            Assert.That(queue.Next, Is.EquivalentTo([tracks[3], tracks[4]]));
        });
    }

    #endregion

    #region GetSnapshot

    [Test]
    public void GetSnapshot_ReflectsCurrentTracksIndexAndLoopMode()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(3);

        tracks.ForEach(queue.Add);
        queue.TryMoveToNext(out _);
        queue.LoopMode = LoopMode.Queue;

        TrackQueue.TrackQueueSnapshot snapshot = queue.GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Tracks, Is.EqualTo(tracks));
            Assert.That(snapshot.CurrentTrackIndex, Is.EqualTo(1));
            Assert.That(snapshot.LoopMode, Is.EqualTo(LoopMode.Queue));
        });
    }

    [Test]
    public void GetSnapshot_EmptyQueue_ReturnsEmptySnapshot()
    {
        TrackQueue queue = new(null);
        TrackQueue.TrackQueueSnapshot snapshot = queue.GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Tracks, Is.Empty);
            Assert.That(snapshot.CurrentTrackIndex, Is.EqualTo(-1));
            Assert.That(snapshot.LoopMode, Is.EqualTo(LoopMode.None));
        });
    }

    [Test]
    public void GetSnapshot_CapsTrackCountAtMaximum()
    {
        TrackQueue queue = new(null);
        CreateTracks(150).ForEach(queue.Add);

        TrackQueue.TrackQueueSnapshot snapshot = queue.GetSnapshot();
        Assert.That(snapshot.Tracks, Has.Count.EqualTo(100));
    }

    [Test]
    public void GetSnapshot_CurrentIndexIsNegativeOne_WhenBeyondCappedRange()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(150);

        tracks.ForEach(queue.Add);

        for (int i = 0; i < 120; i++)
        {
            queue.TryMoveToNext(out _);
        }

        TrackQueue.TrackQueueSnapshot snapshot = queue.GetSnapshot();
        Assert.That(snapshot.CurrentTrackIndex, Is.EqualTo(-1));
    }

    [Test]
    public void GetSnapshot_AfterLooping_DoesNotPersistHistory()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _); // wrap

        TrackQueue.TrackQueueSnapshot snapshot = queue.GetSnapshot();

        TrackQueue restored = new(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(restored.CurrentTrack, Is.SameAs(tracks[0]));
            Assert.That(restored.Previous, Is.Empty);
            Assert.That(restored.Next, Is.EqualTo(new[] { tracks[1] }));
            Assert.That(restored.LoopMode, Is.EqualTo(LoopMode.Queue));
        });
    }

    [Test]
    public void GetSnapshot_AfterRewinding_RestoresCurrentPosition()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(4);

        tracks.ForEach(queue.Add);

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // T2
        queue.TryMoveToNext(out _); // T3
        queue.TryMoveToPrevious(out _); // T2

        TrackQueue.TrackQueueSnapshot snapshot = queue.GetSnapshot();
        TrackQueue restored = new(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(restored.CurrentTrack, Is.SameAs(tracks[2]));
            Assert.That(restored.Previous, Is.EqualTo(new[] { tracks[0], tracks[1] }));
            Assert.That(restored.Next, Is.EqualTo(new[] { tracks[3] }));
        });
    }

    #endregion

    #region Clear

    [Test]
    public void Clear_EmptyQueue_ReturnsFalse()
    {
        TrackQueue queue = new(null);

        Assert.That(queue.Clear(), Is.False);
    }

    [Test]
    public void Clear_PreservesCurrentlyPlayingTrackOnly()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(3);

        tracks.ForEach(queue.Add);
        queue.TryMoveToNext(out _); // T1

        bool cleared = queue.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(cleared, Is.True);
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[1]));
            Assert.That(queue.Next, Is.Empty);
            Assert.That(queue.Previous, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.False);
            Assert.That(queue.HasPreviousTrack, Is.False);
        });
    }

    [Test]
    public void Clear_WithOnlyCurrentTrackPresent_ReturnsFalse()
    {
        TrackQueue queue = new(null);

        queue.Add(CreateTrack("A"));

        Assert.That(queue.Clear(), Is.False);
    }

    [Test]
    public void Clear_WhilePastEnd_ClearsEverythingIncludingLastPlayedTrack()
    {
        TrackQueue queue = new(null);

        CreateTracks(2).ForEach(queue.Add);

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _); // exhausted

        bool cleared = queue.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(cleared, Is.True);
            Assert.That(queue.CurrentTrack, Is.Null);
            Assert.That(queue.Previous, Is.Empty);
            Assert.That(queue.Next, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.False);
            Assert.That(queue.HasPreviousTrack, Is.False);
        });
    }

    [Test]
    public void Clear_ResetsAccumulatedLoopHistory()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(2);

        tracks.ForEach(queue.Add);
        queue.LoopMode = LoopMode.Queue;

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _); // wraps

        queue.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(queue.Previous, Is.Empty);
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[0]));
        });
    }

    [Test]
    public void Clear_AfterRewinding_RemovesForwardAndBackwardNavigation()
    {
        TrackQueue queue = new(null);
        List<Track> tracks = CreateTracks(4);

        tracks.ForEach(queue.Add);

        queue.TryMoveToNext(out _); // T1
        queue.TryMoveToNext(out _); // T2
        queue.TryMoveToNext(out _); // T3
        queue.TryMoveToPrevious(out _); // T2

        queue.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.SameAs(tracks[2]));
            Assert.That(queue.Previous, Is.Empty);
            Assert.That(queue.Next, Is.Empty);
        });
    }

    [Test]
    public void Clear_AfterExhaustion_ThenAdd_StartsWithNewTrack()
    {
        TrackQueue queue = new(null);

        Track a = CreateTrack("A");
        Track b = CreateTrack("B");
        Track c = CreateTrack("C");

        queue.Add(a);
        queue.Add(b);

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _); // exhausted

        queue.Clear();
        queue.Add(c);

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.SameAs(c));
            Assert.That(queue.Previous, Is.Empty);
            Assert.That(queue.Next, Is.Empty);
        });
    }

    #endregion
}