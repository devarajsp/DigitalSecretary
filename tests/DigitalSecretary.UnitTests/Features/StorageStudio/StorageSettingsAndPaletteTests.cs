using System.Drawing;
using DigitalSecretary.Features.StorageStudio;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features.StorageStudio;

/// <summary>Covers <see cref="StorageSettingsStore"/> and <see cref="CategoryPalette"/>.</summary>
public sealed class StorageSettingsAndPaletteTests : IDisposable
{
    private readonly string _data = SpaceTrees.TempFolder();

    public void Dispose() => SpaceTrees.Delete(_data);

    [Fact]
    public void Settings_default_when_nothing_is_saved()
    {
        var settings = new StorageSettingsStore(_data).Load();
        settings.Root.Should().BeEmpty();
        settings.MapDepth.Should().Be(2);
    }

    [Fact]
    public void Settings_round_trip()
    {
        var store = new StorageSettingsStore(_data);
        store.Save(new StorageSettings { Root = SpaceTrees.RootPath, MapDepth = 3 });

        var loaded = store.Load();
        loaded.Root.Should().Be(SpaceTrees.RootPath);
        loaded.MapDepth.Should().Be(3);
    }

    [Fact]
    public void Out_of_range_or_corrupt_settings_fall_back_safely()
    {
        var file = Path.Combine(_data, "storage_studio_settings.json");
        File.WriteAllText(file, "{ \"Root\": \"x\", \"MapDepth\": 99 }");
        new StorageSettingsStore(_data).Load().MapDepth.Should().Be(3);

        File.WriteAllText(file, "{ not json");
        new StorageSettingsStore(_data).Load().MapDepth.Should().Be(2);
    }

    [Fact]
    public void Every_category_has_its_own_colour()
        => Enum.GetValues<FileCategory>().Select(CategoryPalette.For).Should().OnlyHaveUniqueItems();

    [Fact]
    public void Tint_moves_toward_white_and_is_clamped()
    {
        var blue = Color.FromArgb(0, 0x72, 0xB2);
        CategoryPalette.Tint(blue, 0).Should().Be(blue);
        CategoryPalette.Tint(blue, 1).ToArgb().Should().Be(Color.White.ToArgb());
        CategoryPalette.Tint(blue, 5).ToArgb().Should().Be(Color.White.ToArgb());
        CategoryPalette.Tint(blue, -1).Should().Be(blue);
        CategoryPalette.Fill(FileCategory.Picture, 1).Should().Be(CategoryPalette.For(FileCategory.Picture));
        CategoryPalette.RelativeLuminance(CategoryPalette.Fill(FileCategory.Picture, 2))
            .Should().BeGreaterThan(CategoryPalette.RelativeLuminance(CategoryPalette.Fill(FileCategory.Picture, 1)));
    }

    [Fact]
    public void Label_text_is_dark_on_light_and_white_on_dark()
    {
        CategoryPalette.TextOn(Color.White).ToArgb().Should().NotBe(Color.White.ToArgb());
        CategoryPalette.TextOn(Color.Black).ToArgb().Should().Be(Color.White.ToArgb());
        CategoryPalette.RelativeLuminance(Color.White).Should().BeApproximately(1.0, 1e-9);
        CategoryPalette.RelativeLuminance(Color.Black).Should().Be(0);
    }

    [Theory]
    [InlineData(FileCategory.Video)]
    [InlineData(FileCategory.Picture)]
    [InlineData(FileCategory.Audio)]
    [InlineData(FileCategory.Document)]
    [InlineData(FileCategory.Archive)]
    [InlineData(FileCategory.Installer)]
    [InlineData(FileCategory.Code)]
    [InlineData(FileCategory.Other)]
    public void Labels_on_every_tile_colour_meet_large_text_contrast(FileCategory category)
    {
        for (var depth = 1; depth <= 3; depth++)
        {
            var fill = CategoryPalette.Fill(category, depth);
            Contrast(fill, CategoryPalette.TextOn(fill)).Should().BeGreaterThanOrEqualTo(3.0, "WCAG AA for bold/large text at depth {0}", depth);
        }
    }

    private static double Contrast(Color a, Color b)
    {
        var la = CategoryPalette.RelativeLuminance(a);
        var lb = CategoryPalette.RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }
}
