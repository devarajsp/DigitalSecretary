using System.Drawing;
using System.Drawing.Imaging;
using DigitalSecretary.Features.SimilarPictures;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.SimilarPictures;

/// <summary>
/// Integration: the path the window takes, on real files - scan a folder of generated pictures, group
/// them, move the pre-marked ones to the review folder, rescan (the review folder is ignored), then undo.
/// </summary>
public sealed class SimilarPicturesPipelineTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ds-simpipe-" + Guid.NewGuid().ToString("N"));

    public SimilarPicturesPipelineTests() => Directory.CreateDirectory(_root);

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

    private string Save(string relative, Bitmap bmp, ImageFormat format)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (bmp)
        {
            bmp.Save(path, format);
        }
        return path;
    }

    [Fact]
    public void Scan_group_move_rescan_and_undo()
    {
        var original = Save(Path.Combine("2024", "beach.png"), SamplePictureSet.Scene(11, 640, 480), ImageFormat.Png);
        var copy = Path.Combine(_root, "backup", "beach (1).png");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(original, copy);
        var small = Save(Path.Combine("chat", "beach-small.jpg"), Pictures.Resized(SamplePictureSet.Scene(11, 640, 480), 160, 120), ImageFormat.Jpeg);
        var turned = Save(Path.Combine("edited", "beach-turned.png"), Pictures.Turned(SamplePictureSet.Scene(11, 640, 480), RotateFlipType.Rotate90FlipNone), ImageFormat.Png);
        Save(Path.Combine("2024", "hills.png"), SamplePictureSet.Scene(24, 640, 480), ImageFormat.Png);

        var cache = new PictureCache();
        var reader = new PictureReader(new GdiThumbnailProvider());
        var scan = PictureScanner.Run(new PictureScanRequest { Root = _root, Cache = cache, Read = reader.Read, ExcludeFolder = ReviewMover.ReviewFolderFor(_root) });
        scan.Pictures.Should().Be(5);

        var groups = SimilarityFinder.Find([.. cache.Records], new FindOptions(), FileBytes.AreIdentical);
        var g = groups.Should().ContainSingle().Subject;
        // A full-size picture is kept - never the shrunken one or the one named like a copy.
        g.Keeper.Path.Should().BeOneOf(original, turned);
        g.Count.Should().Be(4);
        g.Members.Should().Contain(m => m.Picture.Path == small && m.Kind == MatchKind.SamePicture);
        g.Members.Should().Contain(m => m.Orientation != PictureOrientation.AsIs, "the turned copy matches once turned back");
        g.Members.Should().OnlyContain(m => m.Suggested);
        if (g.Keeper.Path == original)
        {
            g.Members.Single(m => m.Picture.Path == copy).Kind.Should().Be(MatchKind.Exact);
        }

        var marked = g.Members.Select(m => m.Picture.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var moved = ReviewMover.Run(new RemoveRequest { Root = _root, Groups = groups, Marked = marked });
        moved.Removed.Should().Be(3);
        File.Exists(g.Keeper.Path).Should().BeTrue("the keeper never moves");
        g.Members.Select(m => m.Picture.Path).Should().OnlyContain(p => !File.Exists(p));
        File.Exists(Path.Combine(ReviewMover.ReviewFolderFor(_root), "chat", "beach-small.jpg")).Should().BeTrue();

        var rescan = PictureScanner.Run(new PictureScanRequest { Root = _root, Cache = cache, Read = reader.Read, ExcludeFolder = ReviewMover.ReviewFolderFor(_root) });
        rescan.Pictures.Should().Be(2, "moved pictures are gone and the review folder is not scanned");
        rescan.Removed.Should().Be(3);

        ReviewMover.Undo(moved.Batch!).Restored.Should().Be(3);
        g.Members.Select(m => m.Picture.Path).Should().OnlyContain(p => File.Exists(p));
    }
}
