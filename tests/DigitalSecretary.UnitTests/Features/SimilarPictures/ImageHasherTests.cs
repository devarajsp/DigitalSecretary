using System.Drawing;
using DigitalSecretary.Features.SimilarPictures;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.SimilarPictures;

/// <summary>
/// Covers <see cref="ImageHasher"/> by what matters: the same picture altered stays within the
/// "same picture" limits, turned/mirrored copies match exactly at the right orientation, and
/// different pictures stay well beyond even the loosest "similar" limits.
/// </summary>
public sealed class ImageHasherTests
{
    private static readonly Thresholds Balanced = Thresholds.For(Sensitivity.Balanced);
    private static readonly Thresholds Loose = Thresholds.For(Sensitivity.Loose);
    private static readonly Fingerprint Original = Pictures.Print(SamplePictureSet.Scene(11));

    public static TheoryData<string> Variants => ["resized", "jpeg40", "brighter", "crop3"];

    private static Bitmap Variant(string name) => name switch
    {
        "resized" => Pictures.Resized(SamplePictureSet.Scene(11), 160, 120),
        "jpeg40" => Pictures.Jpeg(SamplePictureSet.Scene(11), 40),
        "brighter" => Pictures.Brighter(SamplePictureSet.Scene(11), 1.15f),
        _ => Pictures.Cropped(SamplePictureSet.Scene(11), 3)
    };

    [Fact]
    public void Distance_counts_differing_bits()
    {
        ImageHasher.Distance(0, 0).Should().Be(0);
        ImageHasher.Distance(0b1011, 0b0001).Should().Be(2);
        ImageHasher.Distance(ulong.MaxValue, 0).Should().Be(64);
    }

    [Fact]
    public void The_same_picture_hashes_identically()
    {
        var again = Pictures.Print(SamplePictureSet.Scene(11));
        again.PHash.Should().Be(Original.PHash);
        again.DHash.Should().Be(Original.DHash);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void An_altered_copy_stays_within_the_same_picture_limits(string variant)
    {
        var copy = Pictures.Print(Variant(variant));
        ImageHasher.Distance(Original.PHash, copy.PHash).Should().BeLessThanOrEqualTo(Balanced.SamePHash);
        ImageHasher.Distance(Original.DHash, copy.DHash).Should().BeLessThanOrEqualTo(Balanced.SameDHash);
    }

    [Theory]
    [InlineData(RotateFlipType.Rotate90FlipNone, PictureOrientation.Rotate270)]
    [InlineData(RotateFlipType.Rotate180FlipNone, PictureOrientation.Rotate180)]
    [InlineData(RotateFlipType.Rotate270FlipNone, PictureOrientation.Rotate90)]
    [InlineData(RotateFlipType.RotateNoneFlipX, PictureOrientation.MirrorLeftRight)]
    [InlineData(RotateFlipType.RotateNoneFlipY, PictureOrientation.MirrorTopBottom)]
    public void A_turned_or_mirrored_copy_matches_exactly_once_turned_back(RotateFlipType how, PictureOrientation undo)
    {
        var turned = Pictures.Print(Pictures.Turned(SamplePictureSet.Scene(11), how));

        ImageHasher.Distance(Original.PHash, turned.PHash).Should().BeGreaterThan(Loose.SimilarPHash, "as-is it looks different");
        ImageHasher.Distance(Original.PHash, turned.PHashes[(int)undo]).Should().BeLessThanOrEqualTo(2);
        ImageHasher.Distance(Original.DHash, turned.DHashes[(int)undo]).Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public void Different_pictures_are_never_the_same_picture_and_rarely_similar()
    {
        // Every generated scene uses one template (sky, sun, hills, house), so some pairs genuinely look
        // alike to a person too. Those may be offered as "Similar - check" - never pre-marked - but must
        // stay rare; and no unrelated pair may ever be "Same picture", in any orientation.
        var prints = Enumerable.Range(12, 25).Select(s => Pictures.Print(SamplePictureSet.Scene(s))).Prepend(Original).ToList();
        int pairs = 0, similar = 0;
        for (var i = 0; i < prints.Count; i++)
        {
            for (var j = i + 1; j < prints.Count; j++)
            {
                pairs++;
                var p0 = ImageHasher.Distance(prints[i].PHash, prints[j].PHash);
                var d0 = ImageHasher.Distance(prints[i].DHash, prints[j].DHash);
                if (p0 <= Balanced.SimilarPHash && d0 <= Balanced.SimilarDHash)
                {
                    similar++;
                }
                for (var k = 0; k < 8; k++)
                {
                    var p = ImageHasher.Distance(prints[i].PHash, prints[j].PHashes[k]);
                    var d = ImageHasher.Distance(prints[i].DHash, prints[j].DHashes[k]);
                    (p <= Loose.SamePHash && d <= Loose.SameDHash).Should().BeFalse("scenes {0} and {1} must not be the same picture (p={2}, d={3})", i, j, p, d);
                }
            }
        }
        similar.Should().BeLessThanOrEqualTo(pairs / 50, "at most 2% of unrelated look-alike scenes are offered for checking");
    }

    [Fact]
    public void A_fingerprint_has_eight_orientations_and_a_sharpness()
    {
        Original.PHashes.Should().HaveCount(8);
        Original.DHashes.Should().HaveCount(8);
        Original.Sharpness.Should().BeGreaterThan(0);
    }

    [Fact]
    public void A_blurred_copy_is_less_sharp()
    {
        var blurred = Pictures.Print(Pictures.Resized(Pictures.Resized(SamplePictureSet.Scene(11), 40, 30), 320, 240));
        blurred.Sharpness.Should().BeLessThan(Original.Sharpness);
    }

    [Fact]
    public void Hashing_works_on_any_input_size()
    {
        var tiny = new GrayImage(5, 4, Enumerable.Range(0, 20).Select(i => (float)(i * 12)).ToArray());
        var act = () => ImageHasher.Compute(tiny);
        act.Should().NotThrow();
        ImageHasher.PerceptualHash(tiny).Should().Be(ImageHasher.PerceptualHash(tiny.Resize(32, 32)));
    }

    [Fact]
    public void The_dc_bit_is_never_set()
        => (Original.PHash & 1UL).Should().Be(0UL);
}
