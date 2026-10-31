using System.Drawing;
using System.Drawing.Imaging;
using DigitalSecretary.Features.ReduceImages;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.ReduceImages;

/// <summary>Integration: probe, advise, encode, check and swap real pictures made at test time.</summary>
public sealed class ImageRealFileTests
{
    [Theory]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(90)]
    [InlineData(95)]
    [InlineData(98)]
    public void The_jpeg_quality_is_read_back_exactly(int quality)
    {
        using var dir = new TempDir();
        using var photo = TestPictures.Photo(320, 240);
        var path = TestPictures.SaveJpeg(photo, Path.Combine(dir.Path, "q.jpg"), quality);

        JpegQuality.Estimate(path).Should().BeInRange(quality - 1, quality, "GDI+ writes the standard table of one quality step lower");
    }

    [Fact]
    public void Photos_and_graphics_are_told_apart()
    {
        using var dir = new TempDir();
        using var photo = TestPictures.Photo();
        using var graphic = TestPictures.Graphic();
        var p = ImageProbe.Read(new FileInfo(TestPictures.Save(photo, Path.Combine(dir.Path, "p.png"), ImageFormat.Png)))!;
        var g = ImageProbe.Read(new FileInfo(TestPictures.Save(graphic, Path.Combine(dir.Path, "g.bmp"), ImageFormat.Bmp)))!;

        p.Kind.Should().Be(PictureKind.Photo);
        (p.Format, p.Width, p.Height, p.HasAlpha).Should().Be(("png", 1200, 900, false));
        g.Kind.Should().Be(PictureKind.Graphic);
        g.Format.Should().Be("bmp");
    }

    [Fact]
    public void Transparent_pngs_are_noticed_and_never_made_jpeg()
    {
        using var dir = new TempDir();
        using var bmp = new Bitmap(400, 400, PixelFormat.Format32bppArgb);
        using (var gr = Graphics.FromImage(bmp))
        {
            gr.Clear(Color.Transparent);
            gr.FillEllipse(Brushes.OrangeRed, 50, 50, 300, 300);
        }
        var facts = ImageProbe.Read(new FileInfo(TestPictures.Save(bmp, Path.Combine(dir.Path, "logo.png"), ImageFormat.Png)))!;

        facts.HasAlpha.Should().BeTrue();
        ImageAdvisor.Advise(facts with { Size = 5_000_000, Kind = PictureKind.Photo }, new ImageAdviceOptions()).Verdict.Should().Be(Verdict.Skip);
    }

    [Fact]
    public void Junk_is_not_a_picture()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "x.jpg");
        File.WriteAllText(path, "nope");

        ImageProbe.Read(new FileInfo(path)).Should().BeNull();
    }

    [Fact]
    public void A_camera_jpeg_is_resaved_much_smaller_with_its_date_taken_kept_and_quality_checked()
    {
        using var dir = new TempDir();
        using var photo = TestPictures.Photo(2000, 1500);
        var path = TestPictures.SaveJpeg(photo, Path.Combine(dir.Path, "IMG_1.jpg"), 98, orientation: 6);
        var facts = ImageProbe.Read(new FileInfo(path))!;
        var advice = ImageAdvisor.Advise(facts, new ImageAdviceOptions { MinBytes = 0 });
        var output = Path.Combine(dir.Path, "out.jpg");

        advice.Action.Should().Be(ImageAction.RecompressJpeg);
        ImageEncoder.Encode(facts, advice, ImageQuality.VisuallyLossless, output, null, CancellationToken.None).Should().BeNull();

        new FileInfo(output).Length.Should().BeLessThan(facts.Size * 6 / 10);
        JpegQuality.Estimate(output).Should().BeInRange(89, 90);
        TestPictures.DateTakenOf(output).Should().Be(TestPictures.DateTaken);
        using (var copy = Image.FromFile(output))
        {
            BitConverter.ToUInt16(copy.GetPropertyItem(0x0112)!.Value!, 0).Should().Be(6, "the orientation stays with the unturned pixels");
        }
        ImageQualityCheck.Compare(path, output).Should().BeGreaterThan(ImageQualityCheck.MinimumPsnr(ImageQuality.VisuallyLossless));
    }

    [Fact]
    public void A_photo_png_becomes_a_jpeg_and_a_bmp_graphic_becomes_an_identical_png()
    {
        using var dir = new TempDir();
        using var photo = TestPictures.Photo();
        using var graphic = TestPictures.Graphic();
        var png = ImageProbe.Read(new FileInfo(TestPictures.Save(photo, Path.Combine(dir.Path, "scan.png"), ImageFormat.Png)))!;
        var bmp = ImageProbe.Read(new FileInfo(TestPictures.Save(graphic, Path.Combine(dir.Path, "screen.bmp"), ImageFormat.Bmp)))!;
        var o = new ImageAdviceOptions { MinBytes = 0 };
        var pngAdvice = ImageAdvisor.Advise(png, o);
        var bmpAdvice = ImageAdvisor.Advise(bmp, o);

        pngAdvice.Action.Should().Be(ImageAction.PhotoToJpeg);
        bmpAdvice.Action.Should().Be(ImageAction.LosslessToPng);
        ImageEncoder.Encode(png, pngAdvice, ImageQuality.VisuallyLossless, Path.Combine(dir.Path, "a.jpg"), null, default).Should().BeNull();
        ImageEncoder.Encode(bmp, bmpAdvice, ImageQuality.VisuallyLossless, Path.Combine(dir.Path, "b.png"), null, default).Should().BeNull();

        new FileInfo(Path.Combine(dir.Path, "a.jpg")).Length.Should().BeLessThan(png.Size / 2);
        ImageQualityCheck.Compare(png.Path, Path.Combine(dir.Path, "a.jpg")).Should().BeGreaterThan(38);
        ImageQualityCheck.Compare(bmp.Path, Path.Combine(dir.Path, "b.png")).Should().Be(double.PositiveInfinity, "PNG keeps every pixel");
        new FileInfo(Path.Combine(dir.Path, "b.png")).Length.Should().BeLessThan(bmp.Size / 5);
    }

    [Fact]
    public void Downscaling_keeps_the_aspect_ratio_and_records_the_new_pixel_size()
    {
        using var dir = new TempDir();
        using var photo = TestPictures.Photo(3000, 2000);
        var path = TestPictures.SaveJpeg(photo, Path.Combine(dir.Path, "big.jpg"), 92);
        var facts = ImageProbe.Read(new FileInfo(path))!;
        var advice = ImageAdvisor.Advise(facts, new ImageAdviceOptions { MinBytes = 0, Downscale = true, MaxLongSide = 1500 });
        var output = Path.Combine(dir.Path, "small.jpg");

        (advice.TargetWidth, advice.TargetHeight).Should().Be((1500, 1000));
        advice.Verdict.Should().Be(Verdict.Optional, "shrinking removes detail, so it is never pre-selected");
        ImageEncoder.Encode(facts, advice, ImageQuality.VisuallyLossless, output, null, default).Should().BeNull();

        using var copy = Image.FromFile(output);
        (copy.Width, copy.Height).Should().Be((1500, 1000));
        TestPictures.DateTakenOf(output).Should().Be(TestPictures.DateTaken);
    }

    [Fact]
    public void Webp_and_avif_are_made_by_ffmpeg_when_installed()
    {
        var tools = FfmpegLocator.Find();
        using var dir = new TempDir();
        using var photo = TestPictures.Photo(800, 600);
        var facts = ImageProbe.Read(new FileInfo(TestPictures.SaveJpeg(photo, Path.Combine(dir.Path, "p.jpg"), 95, orientation: 6)))!;
        var webp = ImageAdvisor.Advise(facts, new ImageAdviceOptions { MinBytes = 0, Modern = ModernFormat.WebP });
        var avif = ImageAdvisor.Advise(facts, new ImageAdviceOptions { MinBytes = 0, Modern = ModernFormat.Avif });

        if (tools is null)
        {
            ImageEncoder.Encode(facts, webp, ImageQuality.VisuallyLossless, Path.Combine(dir.Path, "o.webp"), null, default).Should().Contain("ffmpeg");
            return;
        }
        ImageEncoder.Encode(facts, webp, ImageQuality.VisuallyLossless, Path.Combine(dir.Path, "o.webp"), tools, default).Should().BeNull();
        ImageEncoder.Encode(facts, avif, ImageQuality.VisuallyLossless, Path.Combine(dir.Path, "o.avif"), tools, default).Should().BeNull();
        new FileInfo(Path.Combine(dir.Path, "o.webp")).Length.Should().BeGreaterThan(0).And.BeLessThan(facts.Size);
        new FileInfo(Path.Combine(dir.Path, "o.avif")).Length.Should().BeGreaterThan(0).And.BeLessThan(facts.Size);
        var probe = ToolRunner.Run(tools.Ffprobe, ["-v", "error", "-show_entries", "stream=width,height", "-of", "csv=p=0", Path.Combine(dir.Path, "o.webp")], null, default);
        probe.Output.Trim().Should().Be("600,800", "the WebP is stored upright");
    }

    [Fact]
    public void A_full_run_swaps_keeps_dates_and_undo_restores()
    {
        using var dir = new TempDir();
        using var photo = TestPictures.Photo(1600, 1200);
        var path = TestPictures.SaveJpeg(photo, Path.Combine(dir.Path, "IMG_9.JPEG"), 97);
        var when = new DateTime(2019, 7, 14, 10, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, when);
        var original = File.ReadAllBytes(path);
        var facts = ImageProbe.Read(new FileInfo(path))!;
        var ledger = new SavingsLedger(Path.Combine(dir.Path, "data", "h.jsonl"));

        var outcome = ImageReduceRunner.Run(new ImageReduceJob
        {
            Root = dir.Path,
            Candidates = [new ImageCandidate(facts, ImageAdvisor.Advise(facts, new ImageAdviceOptions { MinBytes = 0 }))],
            Ledger = ledger
        });

        outcome.Reduced.Should().Be(1, outcome.Files[0].Note);
        File.Exists(path).Should().BeTrue("a .JPEG keeps its own name");
        new FileInfo(path).Length.Should().BeLessThan(original.Length);
        File.GetLastWriteTimeUtc(path).Should().Be(when);
        File.ReadAllBytes(Path.Combine(dir.Path, ImageReduceRunner.ReviewFolderName, "IMG_9.JPEG")).Should().Equal(original);
        SavingsLedger.Total(ledger.Load()).Saved.Should().Be(outcome.Saved);

        ShrinkSwap.Undo(outcome.Batch!).Should().Be((1, 0));
        File.ReadAllBytes(path).Should().Equal(original);
    }

    [Fact]
    public void Scanning_finds_pictures_and_skips_the_originals_folder()
    {
        using var dir = new TempDir();
        using var photo = TestPictures.Photo(200, 150);
        TestPictures.SaveJpeg(photo, Path.Combine(dir.Path, "a.jpg"), 90);
        Directory.CreateDirectory(Path.Combine(dir.Path, ImageReduceRunner.ReviewFolderName));
        TestPictures.SaveJpeg(photo, Path.Combine(dir.Path, ImageReduceRunner.ReviewFolderName, "old.jpg"), 90);
        File.WriteAllText(ShrinkSwap.TempPathFor(Path.Combine(dir.Path, "b.jpg"), ".jpg"), "half");
        File.WriteAllText(Path.Combine(dir.Path, "notes.txt"), "x");
        var cache = new ImageFactsCache();

        var result = ImageFactsScanner.Run(new ImageFactsScanRequest { Root = dir.Path, Cache = cache });

        result.Items.Should().Be(1);
        cache.Records.Single().JpegQuality.Should().BeInRange(89, 90);
        Directory.GetFiles(dir.Path).Should().NotContain(f => ShrinkSwap.IsTemp(f));
    }
}
