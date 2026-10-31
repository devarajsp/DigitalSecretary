using DigitalSecretary.Features.ReduceVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.ReduceVideos;

public sealed class VideoAdvisorTests
{
    /// <summary>A 1080p30 video of <paramref name="minutes"/> minutes at <paramref name="mbps"/> Mbit/s.</summary>
    internal static VideoFacts Video(double mbps, string codec = "h264", double minutes = 2, int w = 1920, int h = 1080, double fps = 30, bool hdr = false, int depth = 8, bool audio = true)
    {
        var ms = (long)(minutes * 60_000);
        return new VideoFacts
        {
            Path = @"C:\v\" + codec + mbps + ".mp4",
            Size = (long)(mbps * 1000 * ms / 8),
            ModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            DurationMs = ms,
            Width = w,
            Height = h,
            Fps = fps,
            Codec = codec,
            IsHdr = hdr,
            BitDepth = depth,
            HasAudio = audio,
            AudioCodec = audio ? "aac" : ""
        };
    }

    [Fact]
    public void A_generous_phone_video_is_recommended_with_a_big_saving()
    {
        var phone = Video(17);

        var advice = VideoAdvisor.Advise(phone, new VideoAdviceOptions());

        advice.Verdict.Should().Be(Verdict.Recommended);
        advice.SavingFrom(phone.Size).Should().BeGreaterThan(phone.Size / 2);
        advice.Reason.Should().Contain("H.264 at 17.0 Mbit/s").And.Contain("HEVC looks the same");
    }

    [Fact]
    public void A_modest_saving_is_left_to_the_user()
    {
        var advice = VideoAdvisor.Advise(Video(7.5), new VideoAdviceOptions());

        advice.Verdict.Should().Be(Verdict.Optional);
    }

    [Fact]
    public void A_small_saving_below_the_minimum_is_skipped()
    {
        var advice = VideoAdvisor.Advise(Video(6), new VideoAdviceOptions { MinSavingPercent = 25 });

        advice.Verdict.Should().Be(Verdict.Skip);
        advice.Reason.Should().StartWith("Would save only");
        advice.SavingFrom(Video(6).Size).Should().Be(0);
    }

    [Fact]
    public void An_already_compact_video_is_skipped()
    {
        VideoAdvisor.Advise(Video(2), new VideoAdviceOptions()).Reason.Should().StartWith("Already compact");
    }

    [Fact]
    public void A_moderately_sized_hevc_video_is_not_encoded_again()
    {
        var advice = VideoAdvisor.Advise(Video(8, "hevc"), new VideoAdviceOptions());

        advice.Verdict.Should().Be(Verdict.Skip);
        advice.Reason.Should().Contain("Already HEVC");
    }

    [Fact]
    public void A_wasteful_hevc_video_can_still_be_recommended()
    {
        VideoAdvisor.Advise(Video(25, "hevc"), new VideoAdviceOptions()).Verdict.Should().Be(Verdict.Recommended);
    }

    [Fact]
    public void Hdr_is_skipped_by_default_and_offered_when_allowed()
    {
        var hdr = Video(40, "hevc", hdr: true, depth: 10);

        VideoAdvisor.Advise(hdr, new VideoAdviceOptions()).Reason.Should().StartWith("HDR video");
        VideoAdvisor.Advise(hdr, new VideoAdviceOptions { SkipHdr = false }).Verdict.Should().NotBe(Verdict.Skip);
    }

    [Fact]
    public void The_windows_engine_refuses_ten_bit_video()
    {
        var advice = VideoAdvisor.Advise(Video(40, depth: 10), new VideoAdviceOptions { Engine = VideoEngineKind.Windows, SkipHdr = false });

        advice.Reason.Should().Contain("10-bit");
    }

    [Theory]
    [InlineData(1000, 1920, 1080, 30)]
    [InlineData(60_000, 0, 1080, 30)]
    [InlineData(60_000, 1920, 1080, 0)]
    public void Too_short_or_unreadable_videos_are_skipped(long ms, int w, int h, double fps)
    {
        var facts = Video(20) with { DurationMs = ms, Width = w, Height = h, Fps = fps };

        VideoAdvisor.Unsuitable(facts, new VideoAdviceOptions()).Should().StartWith("Too short");
    }

    [Fact]
    public void Smaller_quality_and_better_engines_need_fewer_bits()
    {
        var v = Video(17);
        double Bpp(VideoEngineKind e, VideoQuality q, bool hevc = true) => VideoAdvisor.TargetBitsPerPixel(v, new VideoAdviceOptions { Engine = e, Quality = q, WindowsHasHevc = hevc });

        Bpp(VideoEngineKind.FfmpegX265, VideoQuality.Smallest).Should().BeLessThan(Bpp(VideoEngineKind.FfmpegX265, VideoQuality.Balanced));
        Bpp(VideoEngineKind.FfmpegX265, VideoQuality.Balanced).Should().BeLessThan(Bpp(VideoEngineKind.FfmpegX265, VideoQuality.VisuallyLossless));
        Bpp(VideoEngineKind.FfmpegX265, VideoQuality.VisuallyLossless).Should().BeLessThan(Bpp(VideoEngineKind.FfmpegGpu, VideoQuality.VisuallyLossless));
        Bpp(VideoEngineKind.Windows, VideoQuality.VisuallyLossless).Should().BeLessThan(Bpp(VideoEngineKind.Windows, VideoQuality.VisuallyLossless, hevc: false));
    }

    [Fact]
    public void Bigger_pictures_and_higher_frame_rates_need_fewer_bits_per_pixel()
    {
        var o = new VideoAdviceOptions();

        VideoAdvisor.TargetBitsPerPixel(Video(40, w: 3840, h: 2160), o).Should().BeLessThan(VideoAdvisor.TargetBitsPerPixel(Video(40), o));
        VideoAdvisor.TargetBitsPerPixel(Video(40, fps: 60), o).Should().BeLessThan(VideoAdvisor.TargetBitsPerPixel(Video(40), o));
    }

    [Fact]
    public void The_estimate_is_video_plus_audio_for_the_whole_length()
    {
        var v = Video(17, minutes: 1);

        VideoAdvisor.EstimateBytes(v, 4000).Should().Be((long)((4000 + VideoAdvisor.AudioKbps) * 60_000.0 / 8 * 1.01));
        VideoAdvisor.EstimateBytes(v with { HasAudio = false }, 4000).Should().Be((long)(4000 * 60_000.0 / 8 * 1.01));
    }

    [Fact]
    public void Bit_rates_read_naturally()
    {
        VideoAdvisor.Mbps(17_400).Should().Be("17.4 Mbit/s");
        VideoAdvisor.Mbps(800).Should().Be("800 kbit/s");
        Video(17).BitsPerPixel.Should().BeApproximately(17e6 / (1920 * 1080 * 30), 0.001);
        new VideoFacts { Path = "x" }.BitsPerPixel.Should().Be(0);
        new VideoFacts { Path = "x" }.BitrateKbps.Should().Be(0);
    }

    [Fact]
    public void The_sample_folder_shows_every_kind_of_advice()
    {
        var verdicts = SampleVideoFacts.Create().Select(f => VideoAdvisor.Advise(f, new VideoAdviceOptions()).Verdict).ToList();

        verdicts.Should().Contain(Verdict.Recommended).And.Contain(Verdict.Optional).And.Contain(Verdict.Skip);
        SampleVideoFacts.Create().Should().OnlyContain(f => f.Path.StartsWith(SampleVideoFacts.Root, StringComparison.Ordinal));
    }
}
