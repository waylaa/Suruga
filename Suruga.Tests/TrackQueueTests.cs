using Suruga.Audio;
using Suruga.Audio.Primitives;
using Suruga.Primitives;

namespace Suruga.Tests;

[TestFixture]
internal sealed class TrackQueueTests
{
    private static Track Track(string id)
        => new(TrackPlatform.Local, id, $"file:///{id}.mp3") { Title = id };

    private static Track[] Tracks(params string[] ids)
        => ids.Select(Track).ToArray();

    private static TrackQueue Queue(params string[] ids)
    {
        TrackQueue queue = [];

        foreach (string id in ids)
        {
            queue.Add(Track(id));
        }

        return queue;
    }

    private static void AssertTrack(Track? actual, string expected)
    {
        Assert.That(actual, Is.Not.Null);
        Assert.That(actual.Id, Is.EqualTo(expected));
    }

    private static void AssertTracks(IReadOnlyList<Track> actual, params string[] expected)
        => Assert.That(actual.Select(x => x.Id), Is.EqualTo(expected));

    // ---------------------------------------------------------------------
    // Construction
    // ---------------------------------------------------------------------

    [Test]
    public void Constructor_WithoutSnapshot_CreatesEmptyQueue()
    {
        TrackQueue queue = [];

        Assert.Multiple(() =>
        {
            Assert.That(queue, Is.Empty);
            Assert.That(queue.CurrentTrack, Is.Null);
            Assert.That(queue.QueuedTracks, Is.Empty);
            Assert.That(queue.History, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.False);
            Assert.That(queue.LoopMode, Is.EqualTo(LoopMode.None));
        });
    }

    [Test]
    public void Constructor_WithSnapshot_RestoresState()
    {
        Track[] tracks = Tracks("A", "B", "C");
        TrackQueue queue = new(new TrackQueue.TrackQueueSnapshot(tracks, 1, LoopMode.Queue));

        Assert.Multiple(() =>
        {
            Assert.That(queue, Has.Count.EqualTo(3));
            AssertTrack(queue.CurrentTrack, "B");
            Assert.That(queue.LoopMode, Is.EqualTo(LoopMode.Queue));

            AssertTracks(queue.History, "A");
            AssertTracks(queue.QueuedTracks, "C");
            Assert.That(queue.HasNextTrack, Is.True);
        });
    }

    [Test]
    public void Constructor_WithEmptySnapshot_UsesEmptyState()
    {
        TrackQueue queue = new(new TrackQueue.TrackQueueSnapshot([], 0, LoopMode.Track));

        Assert.Multiple(() =>
        {
            Assert.That(queue, Is.Empty);
            Assert.That(queue.CurrentTrack, Is.Null);
            Assert.That(queue.QueuedTracks, Is.Empty);
            Assert.That(queue.History, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.False);
            Assert.That(queue.LoopMode, Is.EqualTo(LoopMode.Track));
        });
    }

    [Test]
    public void Constructor_WithNegativePersistedIndex_StartsAtFirstTrack()
    {
        TrackQueue queue = new(new TrackQueue.TrackQueueSnapshot(
            Tracks("A", "B", "C"), -1, LoopMode.None));

        AssertTrack(queue.CurrentTrack, "A");
        AssertTracks(queue.QueuedTracks, "B", "C");
        Assert.That(queue.History, Is.Empty);
    }

    [Test]
    public void Constructor_WithIndexGreaterThanCount_LoadsAsExhausted()
    {
        TrackQueue queue = new(new TrackQueue.TrackQueueSnapshot(
            Tracks("A", "B", "C"), 100, LoopMode.None));

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.Null);
            AssertTracks(queue.History, "A", "B", "C");
            Assert.That(queue.QueuedTracks, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.False);
        });
    }

    [Test]
    public void Constructor_WithIndexEqualToCount_LoadsAsExhausted()
    {
        TrackQueue queue = new(new TrackQueue.TrackQueueSnapshot(
            Tracks("A", "B", "C"), 3, LoopMode.None));

        Assert.Multiple(() =>
        {
            Assert.That(queue.CurrentTrack, Is.Null);
            AssertTracks(queue.History, "A", "B", "C");
            Assert.That(queue.QueuedTracks, Is.Empty);
        });
    }

    // ---------------------------------------------------------------------
    // Add
    // ---------------------------------------------------------------------

    [Test]
    public void Add_ToEmptyQueue_StartsFirstTrack()
    {
        TrackQueue queue = [Track("A")];

        Assert.Multiple(() =>
        {
            Assert.That(queue, Has.Count.EqualTo(1));
            AssertTrack(queue.CurrentTrack, "A");
            Assert.That(queue.History, Is.Empty);
            Assert.That(queue.QueuedTracks, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.False);
        });
    }
    
    [Test]
    public void Add_FirstTrack_MakesItCurrent()
    {
        TrackQueue queue = [];
        Track a = Track("A");

        queue.Add(a);

        AssertTrack(queue.CurrentTrack, "A");
        Assert.Multiple(() =>
        {
            Assert.That(queue.History, Is.Empty);
            Assert.That(queue.QueuedTracks, Is.Empty);
        });
    }

    [Test]
    public void Add_WhilePlaying_AppendsAfterCurrent()
    {
        TrackQueue queue = Queue("A");

        queue.Add(Track("B"));
        queue.Add(Track("C"));

        Assert.Multiple(() =>
        {
            AssertTrack(queue.CurrentTrack, "A");
            AssertTracks(queue.QueuedTracks, "B", "C");
            AssertTracks(queue.History);
            Assert.That(queue.HasNextTrack, Is.True);
        });
    }

    [Test]
    public void Add_AfterMovingCurrent_AppendsToEnd()
    {
        TrackQueue queue = Queue("A", "B");

        queue.TryMoveToNext(out _);
        queue.Add(Track("C"));

        AssertTrack(queue.CurrentTrack, "B");
        AssertTracks(queue.History, "A");
        AssertTracks(queue.QueuedTracks, "C");
    }

    [Test]
    public void Add_AfterExhaustion_MakesNewTrackCurrent()
    {
        TrackQueue queue = Queue("A", "B");

        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.True);
        
        Assert.Multiple(() =>
        {
            Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.False);
            Assert.That(queue.CurrentTrack, Is.Null);
        });

        queue.Add(Track("C"));

        Assert.Multiple(() =>
        {
            AssertTrack(queue.CurrentTrack, "C");
            AssertTracks(queue.History, "A", "B");
            AssertTracks(queue.QueuedTracks);
            Assert.That(queue.HasNextTrack, Is.False);
        });
    }

    [Test]
    public void AddRange_ToEmptyQueue_StartsFirstTrack()
    {
        TrackQueue queue = new();
        queue.AddRange(Tracks("A", "B", "C"));

        Assert.Multiple(() =>
        {
            AssertTrack(queue.CurrentTrack, "A");
            AssertTracks(queue.QueuedTracks, "B", "C");
            AssertTracks(queue.History);
        });
    }

    [Test]
    public void AddRange_WhilePlaying_AppendsAllTracks()
    {
        TrackQueue queue = Queue("A");
        queue.AddRange(Tracks("B", "C", "D"));

        Assert.Multiple(() =>
        {
            AssertTrack(queue.CurrentTrack, "A");
            AssertTracks(queue.QueuedTracks, "B", "C", "D");
        });
    }

    [Test]
    public void AddRange_AfterExhaustion_MakesFirstAddedTrackCurrent()
    {
        TrackQueue queue = Queue("A", "B");

        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.True);
        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.False);

        queue.AddRange(Tracks("C", "D", "E"));

        Assert.Multiple(() =>
        {
            AssertTrack(queue.CurrentTrack, "C");
            AssertTracks(queue.History, "A", "B");
            AssertTracks(queue.QueuedTracks, "D", "E");
        });
    }

    [Test]
    public void Add_ThrowsForNull()
    {
        TrackQueue queue = [];
        Assert.Throws<ArgumentNullException>(() => queue.Add(null!));
    }

    [Test]
    public void AddRange_ThrowsForNull()
    {
        TrackQueue queue = [];
        Assert.Throws<ArgumentNullException>(() => queue.AddRange(null!));
    }

    [Test]
    public void AddRange_EmptyCollection_DoesNothing()
    {
        TrackQueue queue = [];

        Assert.Multiple(() =>
        {
            Assert.That(queue, Is.Empty);
            Assert.That(queue.CurrentTrack, Is.Null);
        });
    }

    // ---------------------------------------------------------------------
    // Next
    // ---------------------------------------------------------------------

    [Test]
    public void TryMoveToNext_OnEmptyQueue_ReturnsFalse()
    {
        TrackQueue queue = [];

        bool result = queue.TryMoveToNext(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(track, Is.Null);
            Assert.That(queue.CurrentTrack, Is.Null);
        });
    }

    [Test]
    public void TryMoveToNext_MovesToNextTrack()
    {
        TrackQueue queue = Queue("A", "B", "C");

        bool result = queue.TryMoveToNext(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            AssertTrack(track, "B");
            AssertTrack(queue.CurrentTrack, "B");
            AssertTracks(queue.History, "A");
            AssertTracks(queue.QueuedTracks, "C");
        });
    }

    [Test]
    public void TryMoveToNext_MovesThroughEntireQueue()
    {
        TrackQueue queue = Queue("A", "B", "C");

        Assert.That(queue.TryMoveToNext(out Track? track), Is.True);
        AssertTrack(track, "B");

        Assert.That(queue.TryMoveToNext(out track), Is.True);
        AssertTrack(track, "C");

        Assert.Multiple(() =>
        {
            Assert.That(queue.TryMoveToNext(out track), Is.False);
            Assert.That(track, Is.Null);

            Assert.That(queue.CurrentTrack, Is.Null);
        });
        
        AssertTracks(queue.History, "A", "B", "C");
    }

    [Test]
    public void TryMoveToNext_WhenAlreadyExhausted_ReturnsFalse()
    {
        TrackQueue queue = Queue("A");

        Assert.Multiple(() =>
        {
            Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.False);
            Assert.That(queue.TryMoveToNext(out Track? track), Is.False);

            Assert.That(track, Is.Null);
            Assert.That(queue.CurrentTrack, Is.Null);
        });
    }

    [Test]
    public void TryMoveToNext_WithQueueLoop_WrapsAround()
    {
        TrackQueue queue = Queue("A", "B", "C");
        queue.LoopMode = LoopMode.Queue;

        queue.TryMoveToNext(out Track? track);
        AssertTrack(track, "B");

        queue.TryMoveToNext(out track);
        AssertTrack(track, "C");

        queue.TryMoveToNext(out track);
        AssertTrack(track, "A");

        AssertTrack(queue.CurrentTrack, "A");
    }

    [Test]
    public void TryMoveToNext_WhenExhausted_WithQueueLoop_RestartsAtFirstTrack()
    {
        TrackQueue queue = new(new TrackQueue.TrackQueueSnapshot(
            Tracks("A", "B", "C"), 3, LoopMode.Queue));

        Assert.That(queue.TryMoveToNext(out Track? track), Is.True);

        AssertTrack(track, "A");
        AssertTrack(queue.CurrentTrack, "A");
    }

    // ---------------------------------------------------------------------
    // Previous
    // ---------------------------------------------------------------------

    [Test]
    public void TryMoveToPrevious_OnEmptyQueue_ReturnsFalse()
    {
        TrackQueue queue = [];
        bool result = queue.TryMoveToPrevious(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(track, Is.Null);
        });
    }

    [Test]
    public void TryMoveToPrevious_AtFirstTrack_ReturnsFalse()
    {
        TrackQueue queue = Queue("A", "B", "C");
        bool result = queue.TryMoveToPrevious(out Track? track);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(track, Is.Null);
            AssertTrack(queue.CurrentTrack, "A");
        });
    }

    [Test]
    public void TryMoveToPrevious_MovesBackward()
    {
        TrackQueue queue = Queue("A", "B", "C");

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        AssertTrack(queue.CurrentTrack, "C");

        Assert.That(queue.TryMoveToPrevious(out Track? track), Is.True);

        AssertTrack(track, "B");
        AssertTrack(queue.CurrentTrack, "B");
    }

    [Test]
    public void TryMoveToPrevious_FromExhausted_ReturnsLastTrack()
    {
        TrackQueue queue = Queue("A", "B", "C");

        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.True);
        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.True);
        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.False);
        
        Assert.That(queue.TryMoveToPrevious(out Track? track), Is.True);

        AssertTrack(track, "C");
        AssertTrack(queue.CurrentTrack, "C");
    }

    [Test]
    public void TryMoveToPrevious_WithQueueLoop_WrapsToLastTrack()
    {
        TrackQueue queue = Queue("A", "B", "C");
        queue.LoopMode = LoopMode.Queue;

        Assert.That(queue.TryMoveToPrevious(out Track? track), Is.True);

        AssertTrack(track, "C");
        AssertTrack(queue.CurrentTrack, "C");
    }

    // ---------------------------------------------------------------------
    // Automatic advancement
    // ---------------------------------------------------------------------

    [Test]
    public void TryAdvanceAfterCompletion_OnEmptyQueue_ReturnsFalse()
    {
        TrackQueue queue = [];

        Assert.Multiple(() =>
        {
            Assert.That(queue.TryAdvanceAfterCompletion(out Track? track), Is.False);
            Assert.That(track, Is.Null);
        });
    }

    [Test]
    public void TryAdvanceAfterCompletion_FromEmptyQueue_ReturnsFalse()
    {
        TrackQueue queue = [];

        Assert.Multiple(() =>
        {
            Assert.That(queue.TryAdvanceAfterCompletion(out Track? track), Is.False);
            Assert.That(track, Is.Null);
        });
    }

    [Test]
    public void TryAdvanceAfterCompletion_None_MovesToNextTrack()
    {
        TrackQueue queue = Queue("A", "B", "C");

        Assert.That(queue.TryAdvanceAfterCompletion(out Track? track), Is.True);

        AssertTrack(track, "B");
        AssertTrack(queue.CurrentTrack, "B");
    }

    [Test]
    public void TryAdvanceAfterCompletion_None_ExhaustsQueue()
    {
        TrackQueue queue = Queue("A", "B");

        Assert.That(queue.TryAdvanceAfterCompletion(out Track? track), Is.True);

        AssertTrack(track, "B");

        Assert.Multiple(() =>
        {
            Assert.That(queue.TryAdvanceAfterCompletion(out track), Is.False);

            Assert.That(track, Is.Null);
            Assert.That(queue.CurrentTrack, Is.Null);
        });
        AssertTracks(queue.History, "A", "B");
    }

    [Test]
    public void TryAdvanceAfterCompletion_None_WhenExhausted_RemainsExhausted()
    {
        TrackQueue queue = Queue("A");

        Assert.Multiple(() =>
        {
            Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.False);
            Assert.That(queue.TryAdvanceAfterCompletion(out Track? track), Is.False);

            Assert.That(track, Is.Null);
            Assert.That(queue.CurrentTrack, Is.Null);
        });
    }

    [Test]
    public void TryAdvanceAfterCompletion_Track_ReplaysCurrentTrack()
    {
        TrackQueue queue = Queue("A", "B");
        queue.LoopMode = LoopMode.Track;

        Assert.That(queue.TryAdvanceAfterCompletion(out Track? track), Is.True);

        AssertTrack(track, "A");
        AssertTrack(queue.CurrentTrack, "A");

        Assert.That(queue.TryAdvanceAfterCompletion(out track), Is.True);

        AssertTrack(track, "A");
        AssertTrack(queue.CurrentTrack, "A");
    }

    [Test]
    public void TryAdvanceAfterCompletion_Track_DoesNotAdvance()
    {
        TrackQueue queue = Queue("A", "B");
        queue.TryMoveToNext(out _);

        queue.LoopMode = LoopMode.Track;

        Assert.That(queue.TryAdvanceAfterCompletion(out Track? track), Is.True);

        AssertTrack(track, "B");
        AssertTrack(queue.CurrentTrack, "B");
    }

    [Test]
    public void TryAdvanceAfterCompletion_Queue_WrapsAround()
    {
        TrackQueue queue = Queue("A", "B", "C");
        queue.LoopMode = LoopMode.Queue;

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        AssertTrack(queue.CurrentTrack, "C");

        Assert.That(queue.TryAdvanceAfterCompletion(out Track? track), Is.True);

        AssertTrack(track, "A");
        AssertTrack(queue.CurrentTrack, "A");
    }

    // ---------------------------------------------------------------------
    // HasNextTrack
    // ---------------------------------------------------------------------

    [Test]
    public void HasNextTrack_EmptyQueue_IsFalse()
    {
        TrackQueue queue = [];
        Assert.That(queue.HasNextTrack, Is.False);
    }

    [Test]
    public void HasNextTrack_WithUpcomingTrack_IsTrue()
    {
        TrackQueue queue = Queue("A", "B");
        Assert.That(queue.HasNextTrack, Is.True);
    }

    [Test]
    public void HasNextTrack_AtLastTrack_IsFalseWithoutLoop()
    {
        TrackQueue queue = Queue("A", "B");

        queue.TryMoveToNext(out _);
        Assert.That(queue.HasNextTrack, Is.False);
    }

    [Test]
    public void HasNextTrack_AtLastTrack_IsTrueWithQueueLoop()
    {
        TrackQueue queue = Queue("A", "B");
        queue.LoopMode = LoopMode.Queue;

        queue.TryMoveToNext(out _);

        Assert.That(queue.HasNextTrack, Is.True);
    }

    [Test]
    public void HasNextTrack_WhenExhausted_IsFalseWithoutLoop()
    {
        TrackQueue queue = new(new TrackQueue.TrackQueueSnapshot(
            Tracks("A", "B"), 2, LoopMode.None));

        Assert.That(queue.HasNextTrack, Is.False);
    }

    [Test]
    public void HasNextTrack_WhenExhausted_IsTrueWithQueueLoop()
    {
        TrackQueue queue = new(new TrackQueue.TrackQueueSnapshot(
            Tracks("A", "B"), 2, LoopMode.Queue));

        Assert.That(queue.HasNextTrack, Is.True);
    }

    // ---------------------------------------------------------------------
    // QueuedTracks
    // ---------------------------------------------------------------------

    [Test]
    public void QueuedTracks_EmptyQueue_ReturnsEmpty()
    {
        TrackQueue queue = [];
        Assert.That(queue.QueuedTracks, Is.Empty);
    }

    [Test]
    public void QueuedTracks_DoesNotIncludeCurrentTrack()
    {
        TrackQueue queue = Queue("A", "B", "C");
        AssertTracks(queue.QueuedTracks, "B", "C");
    }

    [Test]
    public void QueuedTracks_ContainsOnlyTracksAfterCurrent()
    {
        TrackQueue queue = Queue("A", "B", "C", "D");
        queue.TryMoveToNext(out _);

        AssertTrack(queue.CurrentTrack, "B");
        AssertTracks(queue.QueuedTracks, "C", "D");
    }

    [Test]
    public void QueuedTracks_AtLastTrack_IsEmpty()
    {
        TrackQueue queue = Queue("A", "B");
        queue.TryMoveToNext(out _);

        Assert.That(queue.QueuedTracks, Is.Empty);
    }

    [Test]
    public void QueuedTracks_WhenExhausted_IsEmpty()
    {
        TrackQueue queue = new(new TrackQueue.TrackQueueSnapshot(
            Tracks("A", "B", "C"), 3, LoopMode.None));

        Assert.That(queue.QueuedTracks, Is.Empty);
    }

    [Test]
    public void QueuedTracks_ReturnsSnapshot()
    {
        TrackQueue queue = Queue("A", "B", "C");
        IReadOnlyList<Track> queued = queue.QueuedTracks;

        queue.Add(Track("D"));

        AssertTracks(queued, "B", "C");
        AssertTracks(queue.QueuedTracks, "B", "C", "D");
    }

    // ---------------------------------------------------------------------
    // History
    // ---------------------------------------------------------------------

    [Test]
    public void History_AtFirstTrack_IsEmpty()
    {
        TrackQueue queue = Queue("A", "B", "C");
        Assert.That(queue.History, Is.Empty);
    }

    [Test]
    public void History_ContainsTracksBeforeCurrent()
    {
        TrackQueue queue = Queue("A", "B", "C");

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        AssertTrack(queue.CurrentTrack, "C");
        AssertTracks(queue.History, "A", "B");
    }

    [Test]
    public void History_AfterMovingBackward_RemovesTracksAfterCurrent()
    {
        TrackQueue queue = Queue("A", "B", "C");

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);
        queue.TryMoveToPrevious(out _);

        AssertTrack(queue.CurrentTrack, "B");
        AssertTracks(queue.History, "A");
        AssertTracks(queue.QueuedTracks, "C");
    }

    [Test]
    public void History_WhenExhausted_ContainsEntireQueue()
    {
        TrackQueue queue = Queue("A", "B", "C");

        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.True);
        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.True);
        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.False);

        AssertTracks(queue.History, "A", "B", "C");
    }

    [Test]
    public void History_ReturnsSnapshot()
    {
        TrackQueue queue = Queue("A", "B", "C");
        queue.TryMoveToNext(out _);

        IReadOnlyList<Track> history = queue.History;
        queue.TryMoveToNext(out _);

        AssertTracks(history, "A");
        AssertTracks(queue.History, "A", "B");
    }

    // ---------------------------------------------------------------------
    // Loop mode
    // ---------------------------------------------------------------------

    [Test]
    public void LoopMode_DefaultsToNone()
    {
        TrackQueue queue = [];
        Assert.That(queue.LoopMode, Is.EqualTo(LoopMode.None));
    }

    [Test]
    public void LoopMode_CanBeChanged()
    {
        TrackQueue queue = new() { LoopMode = LoopMode.Track };

        Assert.That(queue.LoopMode, Is.EqualTo(LoopMode.Track));

        queue.LoopMode = LoopMode.Queue;
        Assert.That(queue.LoopMode, Is.EqualTo(LoopMode.Queue));

        queue.LoopMode = LoopMode.None;
        Assert.That(queue.LoopMode, Is.EqualTo(LoopMode.None));
    }

    // ---------------------------------------------------------------------
    // Shuffle
    // ---------------------------------------------------------------------

    [Test]
    public void Shuffle_EmptyQueue_ReturnsFalse()
    {
        TrackQueue queue = [];
        Assert.That(queue.Shuffle(), Is.False);
    }

    [Test]
    public void Shuffle_SingleTrack_ReturnsFalse()
    {
        TrackQueue queue = Queue("A");
        Assert.That(queue.Shuffle(), Is.False);
    }

    [Test]
    public void Shuffle_TwoTracks_WhenCurrentIsFirst_ReturnsFalse()
    {
        TrackQueue queue = Queue("A", "B");

        // There is only one upcoming track.
        Assert.That(queue.Shuffle(), Is.False);

        AssertTrack(queue.CurrentTrack, "A");
        AssertTracks(queue.QueuedTracks, "B");
    }

    [Test]
    public void Shuffle_KeepsCurrentTrackInPlace()
    {
        TrackQueue queue = Queue("A", "B", "C", "D", "E");
        queue.TryMoveToNext(out _);

        AssertTrack(queue.CurrentTrack, "B");
        Assert.That(queue.Shuffle(), Is.True);
        AssertTrack(queue.CurrentTrack, "B");
    }

    [Test]
    public void Shuffle_PreservesAllUpcomingTracks()
    {
        TrackQueue queue = Queue("A", "B", "C", "D", "E");
        queue.TryMoveToNext(out _);

        Assert.Multiple(() =>
        {
            Assert.That(queue.Shuffle(), Is.True);

            Assert.That(queue.QueuedTracks.Select(x => x.Id), Is.EquivalentTo(["C", "D", "E"]));
        });
    }

    [Test]
    public void Shuffle_DoesNotChangeHistory()
    {
        TrackQueue queue = [];

        Track a = Track("A");
        Track b = Track("B");
        Track c = Track("C");
        Track d = Track("D");

        queue.AddRange([a, b, c, d]);

        Assert.Multiple(() =>
        {
            // Current = A, upcoming = B,C,D.
            Assert.That(queue.TryMoveToNext(out Track? current), Is.True);
            Assert.That(current, Is.SameAs(b));
        });

        // History = A
        // Current = B
        // Upcoming = C,D
        Track[] historyBefore = queue.History.ToArray();
        Track currentBefore = queue.CurrentTrack!;

        Assert.Multiple(() =>
        {
            Assert.That(queue.Shuffle(), Is.True);

            Assert.That(queue.History, Is.EqualTo(historyBefore));
            Assert.That(queue.CurrentTrack, Is.SameAs(currentBefore));
            Assert.That(queue.QueuedTracks, Is.EquivalentTo([c, d]));
        });
    }

    [Test]
    public void Shuffle_WhenExhausted_PreservesTracksAndExhaustedState()
    {
        TrackQueue queue = [];

        Track a = Track("A");
        Track b = Track("B");
        Track c = Track("C");
        Track d = Track("D");

        queue.AddRange([a, b, c, d]);

        // A -> B -> C -> D -> exhausted.
        Assert.That(queue.TryAdvanceAfterCompletion(out var track), Is.True);
        Assert.That(track, Is.SameAs(b));

        Assert.That(queue.TryAdvanceAfterCompletion(out track), Is.True);
        Assert.That(track, Is.SameAs(c));

        Assert.That(queue.TryAdvanceAfterCompletion(out track), Is.True);
        Assert.That(track, Is.SameAs(d));

        Assert.That(queue.TryAdvanceAfterCompletion(out track), Is.False);
        Assert.That(track, Is.Null);

        Assert.That(queue.CurrentTrack, Is.Null);
        Assert.That(queue.HasNextTrack, Is.False);

        Track[] before = queue.ToArray();

        Assert.That(queue.Shuffle(), Is.True);

        // Shuffle may change the order, but not the contents.
        Assert.That(queue.ToArray(), Is.EquivalentTo(before));

        // Shuffle must not make an exhausted queue current again.
        Assert.That(queue.CurrentTrack, Is.Null);
        Assert.That(queue.HasNextTrack, Is.False);

        // An exhausted queue considers every track history.
        Assert.That(queue.History, Is.EquivalentTo(before));
    }

    [Test]
    public void Shuffle_WhenExhausted_PreservesAllTracks()
    {
        TrackQueue queue = new(new TrackQueue.TrackQueueSnapshot(
            Tracks("A", "B", "C", "D"), 4, LoopMode.None));

        Assert.Multiple(() =>
        {
            Assert.That(queue.Shuffle(), Is.True);

            Assert.That(queue.ToArray().Select(x => x.Id), Is.EquivalentTo(["A", "B", "C", "D"]));
        });
    }

    [Test]
    public void Shuffle_WhenOnlyOneUpcomingTrack_ReturnsFalse()
    {
        TrackQueue queue = Queue("A", "B");
        queue.TryMoveToNext(out _);

        Assert.That(queue.Shuffle(), Is.False);
    }

    // ---------------------------------------------------------------------
    // Clear / Reset
    // ---------------------------------------------------------------------

    [Test]
    public void Clear_EmptyQueue_ReturnsFalse()
    {
        TrackQueue queue = [];
        Assert.That(queue.Clear(), Is.False);
    }

    [Test]
    public void Clear_RemovesEverything()
    {
        TrackQueue queue = Queue("A", "B", "C");
        Assert.That(queue.Clear(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(queue, Is.Empty);
            Assert.That(queue.CurrentTrack, Is.Null);
            Assert.That(queue.QueuedTracks, Is.Empty);
            Assert.That(queue.History, Is.Empty);
            Assert.That(queue.HasNextTrack, Is.False);
        });
    }

    [Test]
    public void Clear_ResetsQueueSoNextAddStartsFromFirstTrack()
    {
        TrackQueue queue = Queue("A", "B");
        queue.TryMoveToNext(out _);

        queue.Clear();
        queue.Add(Track("C"));

        Assert.Multiple(() =>
        {
            Assert.That(queue, Has.Count.EqualTo(1));
            AssertTrack(queue.CurrentTrack, "C");
            AssertTracks(queue.History);
            AssertTracks(queue.QueuedTracks);
        });
    }

    [Test]
    public void Reset_EmptyQueue_RemainsEmpty()
    {
        TrackQueue queue = [];
        queue.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(queue, Is.Empty);
            Assert.That(queue.CurrentTrack, Is.Null);
        });
    }

    [Test]
    public void Reset_MovesCurrentToFirstTrack()
    {
        TrackQueue queue = Queue("A", "B", "C");

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        queue.Reset();

        Assert.Multiple(() =>
        {
            AssertTrack(queue.CurrentTrack, "A");
            AssertTracks(queue.History);
            AssertTracks(queue.QueuedTracks, "B", "C");
        });
    }

    [Test]
    public void Reset_AfterExhaustion_RestartsQueue()
    {
        TrackQueue queue = new(new TrackQueue.TrackQueueSnapshot(
            Tracks("A", "B"), 2, LoopMode.None));

        queue.Reset();

        Assert.Multiple(() =>
        {
            AssertTrack(queue.CurrentTrack, "A");
            AssertTracks(queue.History);
            AssertTracks(queue.QueuedTracks, "B");
        });
    }

    // ---------------------------------------------------------------------
    // Snapshot / persistence
    // ---------------------------------------------------------------------

    [Test]
    public void GetSnapshot_ReturnsCompleteState()
    {
        TrackQueue queue = Queue("A", "B", "C");
        queue.LoopMode = LoopMode.Queue;
        queue.TryMoveToNext(out _);

        TrackQueue.TrackQueueSnapshot snapshot = queue.GetSnapshot();

        Assert.Multiple(() =>
        {
            AssertTracks(snapshot.Tracks, "A", "B", "C");
            Assert.That(snapshot.CurrentTrackIndex, Is.EqualTo(1));
            Assert.That(snapshot.LoopMode, Is.EqualTo(LoopMode.Queue));
        });
    }

    [Test]
    public void GetSnapshot_ReturnsIndependentTrackCollection()
    {
        TrackQueue queue = Queue("A", "B");
        TrackQueue.TrackQueueSnapshot snapshot = queue.GetSnapshot();

        queue.Add(Track("C"));

        AssertTracks(snapshot.Tracks, "A", "B");
        AssertTracks(queue, "A", "B", "C");
    }

    [Test]
    public void Snapshot_RoundTripsNormalState()
    {
        TrackQueue original = Queue("A", "B", "C", "D");

        original.TryMoveToNext(out _);
        original.TryMoveToNext(out _);
        original.LoopMode = LoopMode.Queue;

        TrackQueue.TrackQueueSnapshot snapshot = original.GetSnapshot();
        TrackQueue restored = new(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(restored, Has.Count.EqualTo(original.Count));
            AssertTrack(restored.CurrentTrack, "C");
            AssertTracks(restored.History, "A", "B");
            AssertTracks(restored.QueuedTracks, "D");
            Assert.That(restored.LoopMode, Is.EqualTo(LoopMode.Queue));
        });
    }

    [Test]
    public void Snapshot_RoundTripsExhaustedState()
    {
        TrackQueue original = new(new TrackQueue.TrackQueueSnapshot(
            Tracks("A", "B", "C"), 3, LoopMode.None));

        TrackQueue.TrackQueueSnapshot snapshot = original.GetSnapshot();
        TrackQueue restored = new(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(restored.CurrentTrack, Is.Null);
            AssertTracks(restored.History, "A", "B", "C");
            Assert.That(restored.HasNextTrack, Is.False);
        });
    }

    // ---------------------------------------------------------------------
    // IReadOnlyList
    // ---------------------------------------------------------------------

    [Test]
    public void Indexer_ReturnsTrackAtIndex()
    {
        TrackQueue queue = Queue("A", "B", "C");

        Assert.Multiple(() =>
        {
            AssertTrack(queue[0], "A");
            AssertTrack(queue[1], "B");
            AssertTrack(queue[2], "C");
        });
    }

    [Test]
    public void Indexer_InvalidIndex_Throws()
    {
        TrackQueue queue = Queue("A");
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = queue[1]);
    }

    [Test]
    public void Enumerator_ReturnsAllTracksInOrder()
    {
        TrackQueue queue = Queue("A", "B", "C");

        string[] result = queue
            .Select(x => x.Id)
            .ToArray();

        Assert.That(result, Is.EqualTo(new[] { "A", "B", "C" }));
    }

    [Test]
    public void Enumerator_ReturnsSnapshot()
    {
        TrackQueue queue = Queue("A", "B");

        using IEnumerator<Track> enumerator = queue.GetEnumerator();
        
        queue.Add(Track("C"));
        List<string> result = [];

        while (enumerator.MoveNext())
        {
            result.Add(enumerator.Current.Id);
        }

        Assert.That(result, Is.EqualTo(new[] { "A", "B" }));
    }

    // ---------------------------------------------------------------------
    // State-transition integration tests
    // ---------------------------------------------------------------------

    [Test]
    public void NavigationScenario_PlayForwardThenBackward()
    {
        TrackQueue queue = Queue("A", "B", "C", "D");

        AssertTrack(queue.CurrentTrack, "A");

        queue.TryMoveToNext(out _);
        AssertTrack(queue.CurrentTrack, "B");

        queue.TryMoveToNext(out _);
        AssertTrack(queue.CurrentTrack, "C");

        queue.TryMoveToPrevious(out _);
        AssertTrack(queue.CurrentTrack, "B");

        AssertTracks(queue.History, "A");
        AssertTracks(queue.QueuedTracks, "C", "D");
    }

    [Test]
    public void NavigationScenario_ReachEndThenGoBackward()
    {
        TrackQueue queue = Queue("A", "B", "C");

        queue.TryMoveToNext(out _);
        queue.TryMoveToNext(out _);

        Assert.That(queue.TryMoveToNext(out _), Is.False);
        Assert.That(queue.CurrentTrack, Is.Null);
        Assert.That(queue.TryMoveToPrevious(out Track? track), Is.True);

        AssertTrack(track, "C");
        AssertTracks(queue.History, "A", "B");
        AssertTracks(queue.QueuedTracks);
    }

    [Test]
    public void NavigationScenario_ExhaustThenAddNewTrack()
    {
        TrackQueue queue = Queue("A", "B");

        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.True);
        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.False);

        queue.Add(Track("C"));

        Assert.Multiple(() =>
        {
            AssertTrack(queue.CurrentTrack, "C");
            AssertTracks(queue.History, "A", "B");
            AssertTracks(queue.QueuedTracks);
        });
    }

    [Test]
    public void NavigationScenario_ExhaustThenAddMultipleTracks()
    {
        TrackQueue queue = Queue("A", "B");

        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.True);
        Assert.That(queue.TryAdvanceAfterCompletion(out _), Is.False);

        queue.AddRange(Tracks("C", "D", "E"));

        Assert.Multiple(() =>
        {
            AssertTrack(queue.CurrentTrack, "C");
            AssertTracks(queue.History, "A", "B");
            AssertTracks(queue.QueuedTracks, "D", "E");
        });
    }

    [Test]
    public void NavigationScenario_QueueLoopNeverExhausts()
    {
        TrackQueue queue = Queue("A", "B", "C");
        queue.LoopMode = LoopMode.Queue;

        Assert.That(queue.TryAdvanceAfterCompletion(out Track? track), Is.True);
        AssertTrack(track, "B");

        Assert.That(queue.TryAdvanceAfterCompletion(out track), Is.True);
        AssertTrack(track, "C");

        Assert.That(queue.TryAdvanceAfterCompletion(out track), Is.True);
        AssertTrack(track, "A");

        Assert.That(queue.CurrentTrack, Is.Not.Null);
    }

    [Test]
    public void NavigationScenario_TrackLoopNeverAdvances()
    {
        TrackQueue queue = Queue("A", "B", "C");
        queue.LoopMode = LoopMode.Track;

        Assert.That(queue.TryAdvanceAfterCompletion(out Track? track), Is.True);
        AssertTrack(track, "A");

        Assert.That(queue.TryAdvanceAfterCompletion(out track), Is.True);
        AssertTrack(track, "A");

        Assert.That(queue.TryAdvanceAfterCompletion(out track), Is.True);
        AssertTrack(track, "A");

        AssertTrack(queue.CurrentTrack, "A");
        AssertTracks(queue.History);
        AssertTracks(queue.QueuedTracks, "B", "C");
    }

    // ---------------------------------------------------------------------
    // Thread safety
    // ---------------------------------------------------------------------

    [Test]
    public async Task ConcurrentAdds_DoNotLoseTracks()
    {
        TrackQueue queue = [];

        const int taskCount = 8;
        const int tracksPerTask = 100;

        Task[] tasks = Enumerable
            .Range(0, taskCount)
            .Select(taskIndex => Task.Run(() =>
            {
                for (int i = 0; i < tracksPerTask; i++)
                {
                    queue.Add(Track($"{taskIndex}-{i}"));
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.That(queue, Has.Count.EqualTo(taskCount * tracksPerTask));

        Assert.That(queue.Select(x => x.Id).Distinct().Count(), Is.EqualTo(taskCount * tracksPerTask));
    }

    [Test]
    public Task ConcurrentReadsAndWrites_DoNotThrow()
    {
        TrackQueue queue = Queue("A", "B", "C", "D", "E");

        Task[] tasks =
        [
            Task.Run(() =>
            {
                for (int i = 0; i < 1_000; i++)
                {
                    _ = queue.CurrentTrack;
                    _ = queue.QueuedTracks;
                    _ = queue.History;
                    _ = queue.HasNextTrack;
                    _ = queue.Count;
                }
            }),

            Task.Run(() =>
            {
                for (int i = 0; i < 1_000; i++)
                {
                    queue.Add(Track($"A-{i}"));
                }
            }),

            Task.Run(() =>
            {
                for (int i = 0; i < 1_000; i++)
                {
                    queue.TryMoveToNext(out _);
                }
            }),

            Task.Run(() =>
            {
                for (int i = 0; i < 1_000; i++)
                {
                    queue.TryMoveToPrevious(out _);
                }
            })
        ];

        Assert.DoesNotThrowAsync(async () => await Task.WhenAll(tasks));
        return Task.CompletedTask;
    }

    [Test]
    public async Task ConcurrentEnumeration_DoesNotObserveCollectionMutation()
    {
        TrackQueue queue = Queue("A", "B", "C");

        Task[] tasks =
        [
            Task.Run(() =>
            {
                for (int i = 0; i < 1_000; i++)
                {
                    foreach (Track _ in queue)
                    {
                    }
                }
            }),

            Task.Run(() =>
            {
                for (int i = 0; i < 1_000; i++)
                {
                    queue.Add(Track($"X-{i}"));
                }
            })
        ];

        Assert.DoesNotThrowAsync(async () => await Task.WhenAll(tasks));
    }
}
