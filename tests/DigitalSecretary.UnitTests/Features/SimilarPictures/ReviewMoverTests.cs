using DigitalSecretary.Kit;
using DigitalSecretary.Features.SimilarPictures;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.SimilarPictures;

/// <summary>Covers <see cref="ReviewMover"/> - the guarded move to the review folder and its undo.</summary>
public sealed class ReviewMoverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ds-move-" + Guid.NewGuid().ToString("N"));

    public ReviewMoverTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }

    /// <summary>Writes a file and returns a record that matches it on disk.</summary>
    private PictureRecord File_(string relative, string content = "x")
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        var info = new FileInfo(path);
        return new PictureRecord { Path = path, Size = info.Length, ModifiedUtc = info.LastWriteTimeUtc, PHashes = new ulong[8], DHashes = new ulong[8] };
    }

    private static SimilarGroup Group(PictureRecord keeper, params PictureRecord[] others)
        => new(1, keeper, [.. others.Select(o => new GroupMember(o, MatchKind.SamePicture, PictureOrientation.AsIs, 0, 0, true))]);

    private static HashSet<string> Marked(params PictureRecord[] p) => p.Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void The_plan_never_includes_a_keeper_or_an_unmarked_picture()
    {
        var k = File_("k.jpg");
        var a = File_("a.jpg");
        var b = File_("b.jpg");
        var g = Group(k, a, b);

        ReviewMover.Plan([g], Marked(k, a)).Select(p => p.Path).Should().Equal(a.Path);
    }

    [Fact]
    public void Marked_pictures_move_into_the_review_folder_keeping_their_layout()
    {
        var k = File_(Path.Combine("2024", "k.jpg"));
        var a = File_(Path.Combine("Phone", "sub", "a.jpg"));

        var outcome = ReviewMover.Run(new RemoveRequest { Root = _root, Groups = [Group(k, a)], Marked = Marked(a) });

        outcome.Removed.Should().Be(1);
        outcome.Bytes.Should().Be(1);
        File.Exists(a.Path).Should().BeFalse();
        File.Exists(k.Path).Should().BeTrue("the keeper never moves");
        var target = Path.Combine(ReviewMover.ReviewFolderFor(_root), "Phone", "sub", "a.jpg");
        File.Exists(target).Should().BeTrue();
        outcome.Batch!.Entries.Should().ContainSingle().Which.Should().Be(new MoveEntry(a.Path, target, 1));
    }

    [Fact]
    public void A_name_already_in_the_review_folder_gets_a_number_instead_of_being_overwritten()
    {
        var a = File_("a.jpg");
        var existing = Path.Combine(ReviewMover.ReviewFolderFor(_root), "a.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(existing)!);
        File.WriteAllText(existing, "older");

        ReviewMover.TargetFor(_root, a.Path).Should().Be(Path.Combine(ReviewMover.ReviewFolderFor(_root), "a (2).jpg"));
    }

    [Fact]
    public void A_picture_changed_since_the_scan_is_skipped()
    {
        var k = File_("k.jpg");
        var a = File_("a.jpg");
        File.WriteAllText(a.Path, "changed content");

        var outcome = ReviewMover.Run(new RemoveRequest { Root = _root, Groups = [Group(k, a)], Marked = Marked(a) });

        outcome.Should().Match<RemoveOutcome>(o => o.Removed == 0 && o.Skipped == 1 && o.Batch == null);
        File.Exists(a.Path).Should().BeTrue();
    }

    [Fact]
    public void The_recycle_bin_option_calls_the_recycler_and_leaves_no_undo_batch()
    {
        var k = File_("k.jpg");
        var a = File_("a.jpg");
        var recycled = new List<string>();

        var outcome = ReviewMover.Run(new RemoveRequest { Root = _root, Groups = [Group(k, a)], Marked = Marked(a), Action = RemoveAction.RecycleBin, Recycle = recycled.Add });

        recycled.Should().Equal(a.Path);
        outcome.Removed.Should().Be(1);
        outcome.Batch.Should().BeNull();
    }

    [Fact]
    public void A_failing_removal_is_counted_and_the_rest_continue()
    {
        var k = File_("k.jpg");
        var a = File_("a.jpg");
        var b = File_("b.jpg");
        var outcome = ReviewMover.Run(new RemoveRequest
        {
            Root = _root, Groups = [Group(k, a, b)], Marked = Marked(a, b), Action = RemoveAction.RecycleBin,
            Recycle = p => { if (p == a.Path) { throw new IOException("locked"); } }
        });
        outcome.Failed.Should().Be(1);
        outcome.Removed.Should().Be(1);
    }

    [Fact]
    public void Undo_puts_every_picture_back()
    {
        var k = File_("k.jpg");
        var a = File_(Path.Combine("x", "a.jpg"));
        var outcome = ReviewMover.Run(new RemoveRequest { Root = _root, Groups = [Group(k, a)], Marked = Marked(a) });

        var (restored, skipped) = ReviewMover.Undo(outcome.Batch!);

        restored.Should().Be(1);
        skipped.Should().Be(0);
        File.Exists(a.Path).Should().BeTrue();
    }

    [Fact]
    public void Undo_does_not_overwrite_a_file_that_has_taken_the_old_place()
    {
        var k = File_("k.jpg");
        var a = File_("a.jpg");
        var outcome = ReviewMover.Run(new RemoveRequest { Root = _root, Groups = [Group(k, a)], Marked = Marked(a) });
        File.WriteAllText(a.Path, "new file");

        ReviewMover.Undo(outcome.Batch!).Should().Be((0, 1));
        File.ReadAllText(a.Path).Should().Be("new file");
    }

    [Fact]
    public void A_stopped_removal_moves_nothing_more()
    {
        var k = File_("k.jpg");
        var a = File_("a.jpg");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var outcome = ReviewMover.Run(new RemoveRequest { Root = _root, Groups = [Group(k, a)], Marked = Marked(a), Cancel = cts.Token });
        outcome.Cancelled.Should().BeTrue();
        File.Exists(a.Path).Should().BeTrue();
    }

    [Fact]
    public void The_last_batch_round_trips_and_a_bad_file_reads_as_none()
    {
        var file = Path.Combine(_root, "data", "last_move.json");
        var batch = new MoveBatch(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), _root, [new MoveEntry("a", "b", 3)]);

        ReviewMover.SaveLastBatch(file, batch);
        ReviewMover.LoadLastBatch(file)!.Entries.Should().Equal(batch.Entries);

        File.WriteAllText(file, "{ bad");
        ReviewMover.LoadLastBatch(file).Should().BeNull();
        ReviewMover.LoadLastBatch(Path.Combine(_root, "none.json")).Should().BeNull();
    }
}
