using DigitalSecretary.Features.SimilarVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;
using static DigitalSecretary.UnitTests.Features.SimilarVideos.Videos;

namespace DigitalSecretary.UnitTests.Features.SimilarVideos;

/// <summary>Covers <see cref="VideoRecord"/>, <see cref="VideoCache"/> and <see cref="VideoScanner"/> against real temporary folders (with a fake decoder).</summary>
public sealed class VideoRecordAndScannerTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private string Root => _dir.Path;

    private string Touch(string relative, string text = "x")
    {
        var path = Path.Combine(Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>A fake decoder: every file gets the frames of video 1, except names containing "bad" (unreadable).</summary>
    private sealed class FakeDecoder
    {
        public int Calls;

        public VideoSample? Read(string path)
        {
            Interlocked.Increment(ref Calls);
            return path.Contains("bad", StringComparison.OrdinalIgnoreCase) ? null : SampleVideoSet.Sample(SampleVideoSet.Frames(1, 64, 36), 10_000, 640, 360);
        }
    }

    private VideoScanRequest Request(VideoCache cache, FakeDecoder decoder, CancellationToken cancel = default)
        => new() { Root = Root, Cache = cache, Read = decoder.Read, ExcludeFolder = VideoReviewMover.ReviewFolderFor(Root), Cancel = cancel };

    [Theory]
    [InlineData("clip.MP4", true)]
    [InlineData("clip.mov", true)]
    [InlineData("clip.mkv", true)]
    [InlineData("clip.m2ts", true)]
    [InlineData("clip.webm", true)]
    [InlineData("photo.jpg", false)]
    [InlineData("song.mp3", false)]
    [InlineData("noext", false)]
    public void Video_extensions(string name, bool expected) => VideoScanner.IsVideo(name).Should().Be(expected);

    [Fact]
    public void A_mov_next_to_a_still_of_the_same_name_is_a_live_photo_clip()
    {
        var live = Touch("IMG_1001.MOV");
        Touch("IMG_1001.HEIC");
        var plain = Touch("IMG_1002.MOV");
        var mp4 = Touch("IMG_1003.mp4");
        Touch("IMG_1003.jpg");

        VideoScanner.IsLivePhotoCompanion(live).Should().BeTrue();
        VideoScanner.IsLivePhotoCompanion(plain).Should().BeFalse("no still picture beside it");
        VideoScanner.IsLivePhotoCompanion(mp4).Should().BeFalse("only .MOV clips belong to Live Photos");
    }

    [Fact]
    public void Collect_skips_live_photo_clips_and_the_review_folder_unless_told_otherwise()
    {
        Touch("a.mp4");
        Touch("IMG_1001.MOV");
        Touch("IMG_1001.HEIC");
        Touch(Path.Combine(VideoReviewMover.ReviewFolderName, "moved.mp4"));

        VideoScanner.Collect(Root, VideoReviewMover.ReviewFolderFor(Root), true, null, default).Select(f => f.Name).Should().Equal("a.mp4");
        VideoScanner.Collect(Root, VideoReviewMover.ReviewFolderFor(Root), false, null, default).Select(f => f.Name).Should().Equal("a.mp4", "IMG_1001.MOV");
    }

    [Fact]
    public void A_rescan_only_decodes_new_or_changed_videos()
    {
        Touch("a.mp4");
        Touch("b.mp4");
        var cache = new VideoCache();
        var decoder = new FakeDecoder();

        VideoScanner.Run(Request(cache, decoder)).Should().Be(new FileScanResult(2, 2, 0, 0, 0, false));
        VideoScanner.Run(Request(cache, decoder)).Should().Be(new FileScanResult(2, 0, 2, 0, 0, false));
        Touch("b.mp4", "changed content");
        var third = VideoScanner.Run(Request(cache, decoder));

        third.Read.Should().Be(1);
        decoder.Calls.Should().Be(3);
    }

    [Fact]
    public void Unreadable_videos_are_counted_and_gone_ones_pruned_after_a_full_scan_only()
    {
        Touch("a.mp4");
        var gone = Touch("b.mp4");
        Touch("bad.mp4");
        var cache = new VideoCache();
        VideoScanner.Run(Request(cache, new FakeDecoder())).Unreadable.Should().Be(1);
        File.Delete(gone);

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        VideoScanner.Run(Request(cache, new FakeDecoder(), cts.Token)).Removed.Should().Be(0, "a stopped scan has not seen every file");
        cache.Count.Should().Be(2);

        VideoScanner.Run(Request(cache, new FakeDecoder())).Removed.Should().Be(1);
        cache.Records.Select(r => Path.GetFileName(r.Path)).Should().Equal("a.mp4");
    }

    [Fact]
    public void Flat_or_missing_frames_carry_no_hash()
    {
        using var black = new System.Drawing.Bitmap(64, 36);
        VideoRecord.HashOf(GrayImage.FromBitmap(black)).Should().BeNull("a black frame says nothing about the content");
        VideoRecord.HashOf(null).Should().BeNull();
        using var scene = SampleScene.Paint(7, 64, 36);
        VideoRecord.HashOf(GrayImage.FromBitmap(scene))!.P.Should().HaveCount(8);
    }

    [Fact]
    public void A_record_knows_its_bitrate_pixels_and_usable_frames()
    {
        var r = Hashed("a", [0, null, 0, 0, null, 0, 0, 0, 0, 0], size: 100_000_000, durationMs: 100_000, width: 1920, height: 1080);

        r.BitrateKbps.Should().Be(8000, "100 MB over 100 s is 8 Mbit/s");
        r.Pixels.Should().Be(1920L * 1080);
        r.UsableFrames.Should().Be(8);
        (r with { DurationMs = 0 }).BitrateKbps.Should().Be(0);
        r.Matches(100_000_000, Now).Should().BeTrue();
        r.Matches(100_000_001, Now).Should().BeFalse();
    }

    [Fact]
    public void The_cache_saves_compressed_and_loads_only_current_records()
    {
        var file = VideoCache.PathFor(Path.Combine(Root, "data"), Root);
        var cache = new VideoCache();
        cache.Put(Hashed(Path.Combine(Root, "a.mp4"), All(1)));
        cache.Save(file);

        Path.GetFileName(file).Should().EndWith(".json.gz");
        VideoCache.Load(file).Records.Single().Frames.Should().HaveCount(VideoDecoder.FrameCount);

        VideoCache.IsCurrent(Hashed("old", [1, 2, 3])).Should().BeFalse("made with a different sampling plan");
        File.WriteAllText(file, "not gzip");
        VideoCache.Load(file).Count.Should().Be(0);
        VideoCache.Load(Path.Combine(Root, "missing.json.gz")).Count.Should().Be(0);
    }

    [Fact]
    public void Autosave_runs_while_scanning_so_stop_loses_nothing()
    {
        for (var i = 0; i < 5; i++)
        {
            Touch("v" + i + ".mp4");
        }
        var saves = 0;
        var request = new VideoScanRequest { Root = Root, Cache = new VideoCache(), Read = new FakeDecoder().Read, AutoSaveEvery = 2, Parallelism = 1, AutoSave = _ => saves++ };

        VideoScanner.Run(request);

        saves.Should().Be(2);
    }
}
