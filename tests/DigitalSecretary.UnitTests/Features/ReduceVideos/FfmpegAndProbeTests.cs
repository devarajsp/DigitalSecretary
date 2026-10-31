using DigitalSecretary.Features.ReduceVideos;
using DigitalSecretary.Kit;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.ReduceVideos;

public sealed class FfmpegAndProbeTests
{
    private static readonly DateTime When = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string IphoneJson = """
    {
      "streams": [
        { "codec_type": "video", "codec_name": "h264", "width": 1920, "height": 1080, "pix_fmt": "yuv420p",
          "avg_frame_rate": "30000/1001", "r_frame_rate": "30/1", "color_transfer": "bt709",
          "side_data_list": [ { "side_data_type": "Display Matrix", "rotation": -90 } ] },
        { "codec_type": "audio", "codec_name": "aac" },
        { "codec_type": "data", "codec_name": "none" }
      ],
      "format": { "duration": "65.432000" }
    }
    """;

    [Fact]
    public void Ffprobe_json_becomes_facts()
    {
        var f = FfprobeReader.Parse(IphoneJson, @"C:\v\a.mov", 123_000_000, When)!;

        f.Codec.Should().Be("h264");
        (f.Width, f.Height).Should().Be((1920, 1080));
        f.Fps.Should().BeApproximately(29.97, 0.001);
        f.DurationMs.Should().Be(65_432);
        f.HasAudio.Should().BeTrue();
        f.AudioCodec.Should().Be("aac");
        f.Rotation.Should().Be(90);
        f.IsHdr.Should().BeFalse();
        f.BitDepth.Should().Be(8);
        f.ReadBy.Should().Be("ffprobe");
        f.Matches(123_000_000, When).Should().BeTrue();
        f.Matches(123_000_001, When).Should().BeFalse();
    }

    [Fact]
    public void Hdr_ten_bit_and_the_old_rotate_tag_are_read()
    {
        const string json = """
        { "streams": [ { "codec_type": "video", "codec_name": "hevc", "width": 3840, "height": 2160, "pix_fmt": "yuv420p10le",
            "avg_frame_rate": "0/0", "r_frame_rate": "60/1", "color_transfer": "arib-std-b67", "duration": "12.5", "tags": { "rotate": "270" } } ],
          "format": {} }
        """;

        var f = FfprobeReader.Parse(json, "x.mov", 1, When)!;

        f.IsHdr.Should().BeTrue();
        f.BitDepth.Should().Be(10);
        f.Fps.Should().Be(60, "an unusable average frame rate falls back to the stream rate");
        f.DurationMs.Should().Be(12_500, "the stream length is used when the container has none");
        f.Rotation.Should().Be(270);
        f.HasAudio.Should().BeFalse();
    }

    [Theory]
    [InlineData("""{ "streams": [ { "codec_type": "audio", "codec_name": "mp3" } ], "format": { "duration": "200" } }""")]
    [InlineData("""{ "streams": [ { "codec_type": "video", "codec_name": "mjpeg", "disposition": { "attached_pic": 1 } } ], "format": { "duration": "200" } }""")]
    [InlineData("""{ "streams": [ { "codec_type": "video", "codec_name": "h264" } ], "format": {} }""")]
    [InlineData("""{ "format": {} }""")]
    [InlineData("not json")]
    public void Sound_only_cover_art_no_length_and_junk_are_not_videos(string json)
    {
        FfprobeReader.Parse(json, "x", 1, When).Should().BeNull();
    }

    [Theory]
    [InlineData("30000/1001", 29.97)]
    [InlineData("25/1", 25)]
    [InlineData("0/0", 0)]
    [InlineData("24", 24)]
    [InlineData("junk", 0)]
    public void Frame_rates_are_read_from_fractions(string text, double expected)
    {
        FfprobeReader.Rate(text).Should().BeApproximately(expected, 0.01);
    }

    [Theory]
    [InlineData(@"C:\v\a.MP4", ".mp4")]
    [InlineData(@"C:\v\a.mov", ".mov")]
    [InlineData(@"C:\v\a.mkv", ".mkv")]
    [InlineData(@"C:\v\a.webm", ".mkv")]
    [InlineData(@"C:\v\a.avi", ".mp4")]
    [InlineData(@"C:\v\a.MTS", ".mp4")]
    public void The_copy_keeps_modern_containers_and_moves_old_ones_to_mp4(string path, string ext)
    {
        FfmpegVideoArgs.OutputExtension(path).Should().Be(ext);
    }

    [Fact]
    public void Sound_is_copied_when_the_container_allows_and_converted_otherwise()
    {
        var aac = VideoAdvisorTests.Video(10);
        var pcm = aac with { AudioCodec = "pcm_s16le" };

        FfmpegVideoArgs.CanCopyAudio(aac, ".mp4").Should().BeTrue();
        FfmpegVideoArgs.CanCopyAudio(pcm, ".mp4").Should().BeFalse();
        FfmpegVideoArgs.CanCopyAudio(pcm, ".mkv").Should().BeTrue();
        FfmpegVideoArgs.CanCopyAudio(aac with { HasAudio = false }, ".mp4").Should().BeTrue();
        FfmpegVideoArgs.Build(pcm, "o.mp4", VideoQuality.VisuallyLossless, VideoEngineKind.FfmpegX265, GpuEncoder.None)
            .Should().ContainInOrder("-c:a", "aac", "-b:a", "192k");
    }

    [Fact]
    public void The_x265_command_keeps_metadata_tags_for_apple_and_reports_progress()
    {
        var args = FfmpegVideoArgs.Build(VideoAdvisorTests.Video(10), "o.mp4", VideoQuality.Balanced, VideoEngineKind.FfmpegX265, GpuEncoder.None);

        args.Should().ContainInOrder("-map", "0:v:0", "-map", "0:a?", "-map_metadata", "0");
        args.Should().ContainInOrder("-c:v", "libx265", "-preset", "medium", "-crf", "23", "-pix_fmt", "yuv420p");
        args.Should().ContainInOrder("-tag:v", "hvc1").And.ContainInOrder("-c:a", "copy");
        args.Should().ContainInOrder("-movflags", "+faststart+use_metadata_tags");
        args.Should().ContainInOrder("-progress", "pipe:1", "-nostats", "o.mp4");
        args[^1].Should().Be("o.mp4");
    }

    [Fact]
    public void Mkv_keeps_subtitles_and_ten_bit_stays_ten_bit()
    {
        var args = FfmpegVideoArgs.Build(VideoAdvisorTests.Video(10, depth: 10), "o.mkv", VideoQuality.VisuallyLossless, VideoEngineKind.FfmpegX265, GpuEncoder.None);

        args.Should().ContainInOrder("-map", "0:s?", "-c:s", "copy");
        args.Should().Contain("yuv420p10le").And.NotContain("-tag:v").And.NotContain("-movflags");
    }

    [Theory]
    [InlineData(GpuEncoder.Amd, "hevc_amf")]
    [InlineData(GpuEncoder.Nvidia, "hevc_nvenc")]
    [InlineData(GpuEncoder.Intel, "hevc_qsv")]
    [InlineData(GpuEncoder.None, "libx265")]
    public void The_graphics_card_engine_uses_the_detected_encoder(GpuEncoder gpu, string encoder)
    {
        FfmpegVideoArgs.VideoCodecArgs(VideoAdvisorTests.Video(10), VideoQuality.VisuallyLossless, VideoEngineKind.FfmpegGpu, gpu)
            .Should().ContainInOrder("-c:v", encoder);
        FfmpegVideoArgs.EncoderName(gpu).Should().Be(encoder);
        FfmpegVideoArgs.GpuTestArgs(gpu).Should().ContainInOrder("-c:v", encoder, "-f", "null");
    }

    [Fact]
    public void Quality_numbers_rise_for_smaller_files_and_for_the_graphics_card()
    {
        FfmpegVideoArgs.QualityNumber(VideoQuality.VisuallyLossless, VideoEngineKind.FfmpegX265).Should().Be(20);
        FfmpegVideoArgs.QualityNumber(VideoQuality.Balanced, VideoEngineKind.FfmpegX265).Should().Be(23);
        FfmpegVideoArgs.QualityNumber(VideoQuality.Smallest, VideoEngineKind.FfmpegX265).Should().Be(26);
        FfmpegVideoArgs.QualityNumber(VideoQuality.VisuallyLossless, VideoEngineKind.FfmpegGpu).Should().Be(22);
    }

    [Theory]
    [InlineData("out_time_us=12345678", 12345L)]
    [InlineData("out_time_ms=2000000", 2000L)]
    [InlineData("out_time=00:00:02.000000", null)]
    [InlineData("progress=continue", null)]
    [InlineData("out_time_us=N/A", null)]
    [InlineData("garbage", null)]
    public void Progress_lines_give_the_encoded_position(string line, long? ms)
    {
        FfmpegVideoArgs.ProgressMs(line).Should().Be(ms);
    }

    [Fact]
    public void The_ssim_score_is_read_from_the_summary_line()
    {
        const string err = "frame=  90 fps=0.0\n[Parsed_ssim_4 @ 0000] SSIM Y:0.991234 (20.5) U:0.99 (21) V:0.99 (21) All:0.987654 (19.08)\n";

        FfmpegVideoArgs.ParseSsim(err).Should().BeApproximately(0.987654, 1e-9);
        FfmpegVideoArgs.ParseSsim("no score here").Should().BeNull();
        FfmpegVideoArgs.SsimArgs("r.mp4", "o.mp4").Should().ContainInOrder("-i", "r.mp4", "-i", "o.mp4", "-f", "null", "-");
        FfmpegVideoArgs.MinimumSsim(VideoQuality.Smallest).Should().BeLessThan(FfmpegVideoArgs.MinimumSsim(VideoQuality.VisuallyLossless));
    }

    [Fact]
    public void Ffprobe_is_asked_for_quiet_json()
    {
        FfprobeReader.Args("a.mp4").Should().Equal("-v", "error", "-print_format", "json", "-show_format", "-show_streams", "a.mp4");
    }

    [Theory]
    [InlineData("34363248-0000-0010-8000-00aa00389b71", "h264")]
    [InlineData("43564548-0000-0010-8000-00aa00389b71", "hevc")]
    [InlineData("31305641-0000-0010-8000-00aa00389b71", "av1")]
    [InlineData("3334504d-0000-0010-8000-00aa00389b71", "mpeg4")]
    [InlineData("47504a4d-0000-0010-8000-00aa00389b71", "mjpeg")]
    [InlineData("33564d57-0000-0010-8000-00aa00389b71", "wmv3")]
    [InlineData("e06d8026-db46-11cf-b4d1-00805f6cbbea", "mpeg2video")]
    [InlineData("44434241-0000-0010-8000-00aa00389b71", "abcd")]
    public void Windows_codec_ids_map_to_ffmpeg_names(string id, string codec)
    {
        Codecs.FromMediaFoundation(new Guid(id)).Should().Be(codec);
    }

    [Theory]
    [InlineData("h264", "H.264")]
    [InlineData("hevc", "HEVC (H.265)")]
    [InlineData("wmv3", "Windows Media")]
    [InlineData("dvvideo", "DV")]
    [InlineData("", "?")]
    [InlineData("theora", "THEORA")]
    public void Codec_labels_are_friendly(string codec, string label)
    {
        Codecs.Label(codec).Should().Be(label);
    }

    [Fact]
    public void Windows_frame_rates_come_from_packed_fractions()
    {
        MfVideoProbe.Fps((30000, 1001)).Should().BeApproximately(29.97, 0.001);
        MfVideoProbe.Fps((30, 0)).Should().Be(0);
    }
}
