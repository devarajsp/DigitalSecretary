using System.Drawing;
using DigitalSecretary.Features.ReduceImages;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.ReduceImages;

/// <summary>The pure rules of Reduce Size - Images: advice, JPEG tables, photo/graphic, checks, text, settings, the run loop.</summary>
public sealed class ImageRulesTests
{
    private static ImageFacts Jpeg(int quality, long size = 6_000_000, int w = 4000, int h = 3000)
        => new() { Path = @"C:\p\a.jpg", Size = size, Format = "jpeg", Width = w, Height = h, JpegQuality = quality, Kind = PictureKind.Photo };

    private static ImageFacts Lossless(string format, PictureKind kind, long size = 20_000_000, bool alpha = false)
        => new() { Path = @"C:\p\a." + format, Size = size, Format = format, Width = 3000, Height = 2000, Kind = kind, HasAlpha = alpha };

    [Fact]
    public void A_high_quality_camera_jpeg_is_recommended_and_an_already_compressed_one_skipped()
    {
        var camera = ImageAdvisor.Advise(Jpeg(96), new ImageAdviceOptions());
        var whatsapp = ImageAdvisor.Advise(Jpeg(80, 1_500_000), new ImageAdviceOptions());

        camera.Should().Match<ImageAdvice>(a => a.Verdict == Verdict.Recommended && a.Action == ImageAction.RecompressJpeg && a.Extension == ".jpg");
        camera.Reason.Should().Contain("quality ~96").And.Contain("quality 90 looks the same");
        whatsapp.Verdict.Should().Be(Verdict.Skip);
        whatsapp.Reason.Should().Contain("would add loss");
    }

    [Fact]
    public void An_unknown_jpeg_quality_is_treated_as_92()
    {
        ImageAdvisor.Advise(Jpeg(0), new ImageAdviceOptions()).Reason.Should().Contain("quality ~92");
    }

    [Fact]
    public void Switching_jpeg_recompression_off_skips_jpegs_unless_they_are_shrunk()
    {
        var off = new ImageAdviceOptions { RecompressJpegs = false };

        ImageAdvisor.Advise(Jpeg(96), off).Reason.Should().Contain("switched off");
        ImageAdvisor.Advise(Jpeg(80), off with { Downscale = true, MaxLongSide = 2000 }).Action.Should().Be(ImageAction.RecompressJpeg);
    }

    [Fact]
    public void Photos_stored_lossless_become_jpegs_and_graphics_become_png_or_stay()
    {
        var o = new ImageAdviceOptions();

        ImageAdvisor.Advise(Lossless("png", PictureKind.Photo), o).Action.Should().Be(ImageAction.PhotoToJpeg);
        ImageAdvisor.Advise(Lossless("bmp", PictureKind.Graphic), o).Action.Should().Be(ImageAction.LosslessToPng);
        ImageAdvisor.Advise(Lossless("tiff", PictureKind.Photo), o with { PhotosToJpeg = false }).Action.Should().Be(ImageAction.LosslessToPng);
        ImageAdvisor.Advise(Lossless("png", PictureKind.Graphic), o).Reason.Should().Contain("graphic or screenshot");
        ImageAdvisor.Advise(Lossless("png", PictureKind.Photo, alpha: true), o).Reason.Should().Contain("see-through");
        ImageAdvisor.Advise(Lossless("bmp", PictureKind.Graphic), o with { LosslessToPng = false }).Verdict.Should().Be(Verdict.Skip);
        ImageAdvisor.Advise(Lossless("png", PictureKind.Unknown), o).Reason.Should().Contain("not a photo");
    }

    [Fact]
    public void Downscaling_and_modern_formats_are_never_pre_selected()
    {
        var shrink = ImageAdvisor.Advise(Jpeg(96), new ImageAdviceOptions { Downscale = true, MaxLongSide = 2000 });
        var webp = ImageAdvisor.Advise(Jpeg(96), new ImageAdviceOptions { Modern = ModernFormat.WebP });
        var avif = ImageAdvisor.Advise(Lossless("png", PictureKind.Photo), new ImageAdviceOptions { Modern = ModernFormat.Avif });

        shrink.Verdict.Should().Be(Verdict.Optional);
        (shrink.TargetWidth, shrink.TargetHeight).Should().Be((2000, 1500));
        shrink.Reason.Should().Contain("shrunk to 2000x1500");
        webp.Should().Match<ImageAdvice>(a => a.Verdict == Verdict.Optional && a.Extension == ".webp" && a.Action == ImageAction.ToModern);
        avif.Extension.Should().Be(".avif");
        avif.EstimatedBytes.Should().BeLessThan(webp.EstimatedBytes);
        ImageAdvisor.Advise(Jpeg(70, 2_000_000), new ImageAdviceOptions { Modern = ModernFormat.WebP }).EstimatedBytes.Should().BeLessThan(2_000_000);
        ImageAdvisor.Advise(Lossless("png", PictureKind.Graphic), new ImageAdviceOptions { Modern = ModernFormat.WebP }).Action.Should().NotBe(ImageAction.ToModern, "graphics stay lossless");
    }

    [Fact]
    public void Small_untouchable_unreadable_and_low_saving_pictures_are_skipped()
    {
        var o = new ImageAdviceOptions();

        ImageAdvisor.Advise(Jpeg(98, 100_000, 300, 200), o).Reason.Should().StartWith("Already small");
        ImageAdvisor.Advise(Jpeg(98) with { Untouchable = "CMYK print file - kept as it is." }, o).Reason.Should().StartWith("CMYK");
        ImageAdvisor.Advise(Jpeg(98) with { Width = 0 }, o).Reason.Should().StartWith("Could not read");
        ImageAdvisor.Advise(Jpeg(93, 3_800_000), o with { MinSavingPercent = 40 }).Reason.Should().StartWith("Would save only");
        ImageAdvisor.Advise(Jpeg(93, 3_800_000), o with { MinSavingPercent = 40 }).SavingFrom(3_800_000).Should().Be(0);
    }

    [Fact]
    public void Quality_choices_map_to_jpeg_numbers_and_bits_fall_with_quality()
    {
        ImageAdvisor.TargetJpegQuality(ImageQuality.VisuallyLossless).Should().Be(90);
        ImageAdvisor.TargetJpegQuality(ImageQuality.Balanced).Should().Be(85);
        ImageAdvisor.TargetJpegQuality(ImageQuality.Smallest).Should().Be(80);
        var qualities = new[] { 99, 96, 93, 90, 88, 86, 82, 76, 60 };
        qualities.Select(ImageAdvisor.JpegBitsPerPixel).Should().BeInDescendingOrder();
        ImageAdvisor.TargetSize(Jpeg(90, w: 3000, h: 4000), new ImageAdviceOptions { Downscale = true, MaxLongSide = 2000 }).Should().Be((1500, 2000));
        ImageAdvisor.TargetSize(Jpeg(90), new ImageAdviceOptions { Downscale = true, MaxLongSide = 0 }).Should().Be((4000, 3000));
    }

    [Fact]
    public void The_ijg_table_and_its_inverse_agree_for_every_quality()
    {
        for (var q = 1; q <= 100; q++)
        {
            var table = JpegQuality.TableFor(q);
            table.Should().OnlyContain(v => v >= 1 && v <= 255);
            JpegQuality.TableFor(JpegQuality.BestQuality(table)).Should().Equal(table);
        }
        JpegQuality.TableFor(100).Should().OnlyContain(v => v == 1);
    }

    [Fact]
    public void Quantisation_tables_are_found_in_8_and_16_bit_form_and_junk_gives_zero()
    {
        static byte[] Jpeg(byte[] dqt) => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xDB, .. Enumerable.Reverse(BitConverter.GetBytes((ushort)(dqt.Length + 2))), .. dqt, 0xFF, 0xDA, 0x00, 0x02];
        var q75 = JpegQuality.TableFor(75);
        byte[] eight = [0x00, .. q75.Select(v => (byte)v)];
        byte[] sixteen = [0x10, .. q75.SelectMany(v => new[] { (byte)0, (byte)v })];
        byte[] chromaFirst = [0x01, .. Enumerable.Repeat((byte)99, 64), 0x00, .. q75.Select(v => (byte)v)];

        JpegQuality.Estimate(Jpeg(eight)).Should().Be(75);
        JpegQuality.Estimate(Jpeg(sixteen)).Should().Be(75);
        JpegQuality.Estimate(Jpeg(chromaFirst)).Should().Be(75);
        JpegQuality.Estimate(Jpeg([0x00, 1, 2, 3])).Should().Be(0, "a truncated table is not trusted");
        JpegQuality.Estimate([0x89, 0x50, 0x4E, 0x47]).Should().Be(0);
        JpegQuality.Estimate([0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x02]).Should().Be(0);
        JpegQuality.Estimate([0xFF, 0xD8, 0x12, 0x34, 0x00, 0x02]).Should().Be(0);
    }

    [Fact]
    public void Many_colours_mean_photo_and_flat_runs_mean_graphic()
    {
        var random = new Random(3);
        var photo = Enumerable.Range(0, 96 * 72).Select(_ => random.Next(0x1000000)).ToArray();
        var graphic = Enumerable.Range(0, 96 * 72).Select(i => i % 96 < 40 ? 0xFFFFFF : 0x3366AA).ToArray();

        ImageProbe.Classify(photo, 96).Should().Be(PictureKind.Photo);
        ImageProbe.Classify(graphic, 96).Should().Be(PictureKind.Graphic);
        ImageProbe.Classify([1, 2, 3], 3).Should().Be(PictureKind.Unknown);
        ImageProbe.IsPicture("x.JFIF").Should().BeTrue();
        ImageProbe.IsPicture("x.heic").Should().BeFalse("HEIC is already efficient");
    }

    [Theory]
    [InlineData(1, RotateFlipType.RotateNoneFlipNone)]
    [InlineData(3, RotateFlipType.Rotate180FlipNone)]
    [InlineData(6, RotateFlipType.Rotate90FlipNone)]
    [InlineData(8, RotateFlipType.Rotate270FlipNone)]
    [InlineData(2, RotateFlipType.RotateNoneFlipX)]
    [InlineData(4, RotateFlipType.Rotate180FlipX)]
    [InlineData(5, RotateFlipType.Rotate90FlipX)]
    [InlineData(7, RotateFlipType.Rotate270FlipX)]
    public void Exif_orientation_maps_to_the_upright_turn(int orientation, RotateFlipType turn)
    {
        ImageEncoder.Upright(orientation).Should().Be(turn);
    }

    [Fact]
    public void Modern_format_commands_use_webp_or_still_avif()
    {
        ImageEncoder.ModernArgs("in.png", "o.webp", ImageQuality.VisuallyLossless).Should().ContainInOrder("-c:v", "libwebp", "-quality", "88");
        ImageEncoder.ModernArgs("in.png", "o.avif", ImageQuality.Smallest).Should().ContainInOrder("-c:v", "libaom-av1", "-still-picture", "1", "-crf", "30");
    }

    [Fact]
    public void Psnr_measures_difference_and_rejects_mismatched_sizes()
    {
        byte[] a = [10, 20, 30, 40];

        ImageQualityCheck.Psnr(a, a).Should().Be(double.PositiveInfinity);
        ImageQualityCheck.Psnr(a, [11, 21, 31, 41]).Should().BeApproximately(48.13, 0.01);
        var act = () => ImageQualityCheck.Psnr(a, [1, 2]);
        act.Should().Throw<ArgumentException>();
        ImageQualityCheck.MinimumPsnr(ImageQuality.Smallest).Should().BeLessThan(ImageQualityCheck.MinimumPsnr(ImageQuality.VisuallyLossless));
        ImageQualityCheck.Compare(@"C:\nope\a.jpg", @"C:\nope\b.jpg").Should().BeNull();
    }

    [Fact]
    public void The_copy_check_needs_a_real_saving_and_good_quality_for_same_size_copies()
    {
        var c = new ImageCandidate(Jpeg(96, 1000), ImageAdvisor.Advise(Jpeg(96), new ImageAdviceOptions()) with { TargetWidth = 4000 });

        ImageReduceRunner.Verify(c, "x", 900, ImageQuality.VisuallyLossless, 20, (_, _) => 50).Should().StartWith("Saved only 10%");
        ImageReduceRunner.Verify(c, "x", 500, ImageQuality.VisuallyLossless, 20, (_, _) => 30).Should().StartWith("Picture quality check failed (PSNR 30.0 dB)");
        ImageReduceRunner.Verify(c, "x", 500, ImageQuality.VisuallyLossless, 20, (_, _) => null).Should().StartWith("The copy could not be opened");
        ImageReduceRunner.Verify(c, "x", 500, ImageQuality.VisuallyLossless, 20, (_, _) => 42).Should().BeNull();
        ImageReduceRunner.Verify(c with { Advice = c.Advice with { TargetWidth = 2000 } }, "x", 500, ImageQuality.VisuallyLossless, 20, (_, _) => 1).Should().BeNull("shrunk copies are not compared pixel by pixel");
        ImageReduceRunner.KeepJpegSpelling(@"C:\a.JPEG", ".jpg").Should().Be(".JPEG");
        ImageReduceRunner.KeepJpegSpelling(@"C:\a.png", ".jpg").Should().Be(".jpg");
        ImageReduceRunner.KeepJpegSpelling(@"C:\a.bmp", ".png").Should().Be(".png");
    }

    [Fact]
    public void The_run_loop_counts_failures_and_skips_and_stops()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "a.bmp");
        File.WriteAllBytes(path, new byte[10_000]);
        var info = new FileInfo(path);
        var facts = new ImageFacts { Path = path, Size = 10_000, ModifiedUtc = info.LastWriteTimeUtc, Format = "bmp", Width = 10, Height = 10, Kind = PictureKind.Graphic };
        var advice = ImageAdvisor.Advise(facts, new ImageAdviceOptions { MinBytes = 0 });
        var job = new ImageReduceJob { Root = dir.Path, Candidates = [new ImageCandidate(facts, advice)] };
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var failed = ImageReduceRunner.Run(job with { Encode = (_, _) => "boom" });
        var tooBig = ImageReduceRunner.Run(job with { Encode = (_, o) => { File.WriteAllBytes(o, new byte[9_500]); return null; } });
        var stopped = ImageReduceRunner.Run(job with { Cancel = cts.Token });

        failed.Failed.Should().Be(1);
        tooBig.Skipped.Should().Be(1);
        stopped.Cancelled.Should().BeTrue();
        stopped.Files.Should().BeEmpty();
        Directory.GetFiles(dir.Path).Should().Equal(path);
        ImageReduceRunner.Describe(failed.Files[0]).Should().Be("Failed " + path + ": boom");
        ImageReduceRunner.Describe(tooBig.Files[0]).Should().StartWith("Skipped");
        ImageReduceRunner.Describe(new FileOutcome("p", FileOutcomeKind.Reduced, 10, 5, "")).Should().StartWith("Reduced p:");
    }

    [Fact]
    public void Settings_round_trip_clamp_and_fall_back()
    {
        using var dir = new TempDir();
        var store = new ReduceImagesSettingsStore(dir.Path);
        store.Save(new ReduceImagesSettings { Root = "r", Quality = ImageQuality.Balanced, RecompressJpegs = false, PhotosToJpeg = false, LosslessToPng = false, Downscale = true, MaxLongSide = 2000, Modern = ModernFormat.Avif, MinSavingPercent = 30, Action = RemoveAction.RecycleBin });

        var o = store.Load().ToAdviceOptions();

        o.Should().Be(new ImageAdviceOptions { Quality = ImageQuality.Balanced, RecompressJpegs = false, PhotosToJpeg = false, LosslessToPng = false, Downscale = true, MaxLongSide = 2000, Modern = ModernFormat.Avif, MinSavingPercent = 30 });
        store.Load().Action.Should().Be(RemoveAction.RecycleBin);
        new ReduceImagesSettings { MaxLongSide = 10, MinSavingPercent = 0 }.ToAdviceOptions().Should().Match<ImageAdviceOptions>(x => x.MaxLongSide == 640 && x.MinSavingPercent == 5);
        File.WriteAllText(Path.Combine(dir.Path, "reduce_images_settings.json"), "{bad");
        store.Load().Modern.Should().Be(ModernFormat.None);
    }

    [Fact]
    public void Text_explains_options_rows_and_runs()
    {
        var rows = SampleImageFacts.Create().Select(f => (f, ImageAdvisor.Advise(f, new ImageAdviceOptions()))).ToList();
        var verdicts = rows.Select(r => r.Item2.Verdict).ToList();

        verdicts.Should().Contain(Verdict.Recommended).And.Contain(Verdict.Skip);
        ReduceImagesText.Help(new ImageAdviceOptions(), true).Should().Contain("Screenshots and see-through pictures are never made JPEG");
        ReduceImagesText.Help(new ImageAdviceOptions { Downscale = true, Modern = ModernFormat.WebP }, false).Should().Contain("Downscaling is ON").And.Contain("WebP is ON").And.Contain(FfmpegLocator.InstallCommand);
        ReduceImagesText.Help(new ImageAdviceOptions { Quality = ImageQuality.Smallest }, true).Should().Contain("Smallest");
        ReduceImagesText.Help(new ImageAdviceOptions { Quality = ImageQuality.Balanced, Modern = ModernFormat.Avif }, true).Should().Contain("AVIF is ON").And.Contain("Balanced");
        ReduceImagesText.Summary(rows).Should().StartWith("10 pictures");
        ReduceImagesText.Selection([]).Should().Be("Nothing selected.");
        ReduceImagesText.Selection([rows[0]]).Should().StartWith("1 selected");
        ReduceImagesText.Cells(rows[0].f, rows[0].Item2).Should().HaveCount(9);
        ReduceImagesText.Format(SampleImageFacts.Create()[0]).Should().Be("JPEG q96");
        ReduceImagesText.Format(SampleImageFacts.Create()[3]).Should().Be("PNG photo");
        ReduceImagesText.Format(SampleImageFacts.Create()[5]).Should().Be("BMP graphic");
        ReduceImagesText.Format(SampleImageFacts.Create()[8]).Should().Be("TIFF");
        ReduceImagesText.VerdictLabel(Verdict.Optional).Should().Be("Your call");
        ReduceImagesText.Percent(1, 0).Should().Be("0%");
        foreach (var a in new[] { ImageAction.RecompressJpeg, ImageAction.PhotoToJpeg, ImageAction.LosslessToPng, ImageAction.ToModern, ImageAction.None })
        {
            ReduceImagesText.ActionLabel(new ImageAdvice(Verdict.Optional, a, ".webp", 1, a == ImageAction.RecompressJpeg ? "x; shrunk to 1x1" : "", 1, 1)).Should().NotBeNull();
        }
        ReduceImagesText.SavedSoFar(new SavingsTotal(0, 0, 0)).Should().Be("Nothing reduced yet.");
        ReduceImagesText.SavedSoFar(new SavingsTotal(2, 3000, 1000)).Should().StartWith("Saved so far: 2 pictures");
        var outcome = new ImageReduceOutcome([new FileOutcome("a", FileOutcomeKind.Reduced, 100, 40, ""), new FileOutcome("b", FileOutcomeKind.Skipped, 1, 1, ""), new FileOutcome("c", FileOutcomeKind.Failed, 1, 1, "")], null, true);
        ReduceImagesText.RunSummary(outcome, RemoveAction.ReviewFolder).Should().Contain("Reduced 1").And.Contain("1 kept").And.Contain("1 failed").And.Contain("Stopped").And.Contain(ImageReduceRunner.ReviewFolderName);
        ReduceImagesText.RunSummary(outcome, RemoveAction.RecycleBin).Should().Contain("Recycle Bin");
        var report = ReduceImagesText.Report(SampleImageFacts.Root, rows, new ImageAdviceOptions { Downscale = true }, new DateTime(2026, 11, 1));
        report.Should().StartWith("Reduce Size - Images report").And.Contain("downscale 3840 px");
        SampleImageFacts.Create().Should().OnlyContain(f => f.Path.StartsWith(SampleImageFacts.Root, StringComparison.Ordinal));
    }

    [Fact]
    public void The_cache_keeps_only_usable_records()
    {
        using var dir = new TempDir();
        var file = ImageFactsCache.PathFor(dir.Path, @"C:\p");
        var cache = new ImageFactsCache();
        cache.Put(Jpeg(90));
        cache.Put(Jpeg(90) with { Path = "bad", Width = 0 });
        cache.Save(file);

        ImageFactsCache.Load(file).Records.Select(r => r.Path).Should().Equal(@"C:\p\a.jpg");
        File.WriteAllText(file, "x");
        ImageFactsCache.Load(file).Count.Should().Be(0);
        Jpeg(90).Matches(6_000_000, default).Should().BeTrue();
        Jpeg(90).BitsPerPixel.Should().Be(4.0);
        new ImageFacts { Path = "x" }.BitsPerPixel.Should().Be(0);
    }
}
