using DigitalSecretary.Features.DeDupeFiles;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

/// <summary>Covers the plain-text checksum list format: formatting, tolerant parsing, read and write.</summary>
public sealed class ChecksumListFileTests
{
    private const string HashA = "900150983cd24fb0d6963f7d28e17f72";
    private const string HashB = "d41d8cd98f00b204e9800998ecf8427e";

    private static DateTime Stamp(long extraTicks = 1234567)
        => new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(extraTicks);

    [Fact]
    public void Format_then_parse_round_trips_every_field()
    {
        var entry = new ChecksumEntry(HashA, 4096, Stamp(), @"C:\pics\holiday\a.jpg");

        var parsed = ChecksumListFile.ParseLine(ChecksumListFile.FormatLine(entry));

        parsed.Should().NotBeNull();
        parsed!.Md5.Should().Be(HashA);
        parsed.Length.Should().Be(4096);
        parsed.FullPath.Should().Be(@"C:\pics\holiday\a.jpg");
        parsed.ModifiedUtc.Should().Be(Stamp());
        parsed.ModifiedUtc.Ticks.Should().Be(Stamp().Ticks);
        parsed.HasFileStamp.Should().BeTrue();
    }

    [Fact]
    public void Format_line_writes_four_tab_separated_columns()
    {
        var line = ChecksumListFile.FormatLine(new ChecksumEntry(HashA, 7, Stamp(0), @"C:\a\b.txt"));

        line.Split('\t').Should().HaveCount(4);
        line.Should().StartWith(HashA + "\t7\t");
        line.Should().EndWith("\t" + @"C:\a\b.txt");
    }

    [Fact]
    public void Parse_line_reads_a_four_column_line()
    {
        var entry = ChecksumListFile.ParseLine(HashA + "\t123\t2026-01-02T03:04:05.0000000Z\t" + @"D:\x\y.bin");

        entry.Should().NotBeNull();
        entry!.Length.Should().Be(123);
        entry.ModifiedUtc.Should().Be(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        entry.FullPath.Should().Be(@"D:\x\y.bin");
    }

    [Fact]
    public void Parse_line_accepts_a_legacy_two_column_line_without_a_stamp()
    {
        var entry = ChecksumListFile.ParseLine(HashA + "\t" + @"C:\old\list\a.jpg");

        entry.Should().NotBeNull();
        entry!.Length.Should().Be(-1);
        entry.ModifiedUtc.Should().Be(default);
        entry.HasFileStamp.Should().BeFalse();
        entry.FullPath.Should().Be(@"C:\old\list\a.jpg");
    }

    [Theory]
    [InlineData("# a comment")]
    [InlineData("   # indented comment")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\t")]
    [InlineData("just-one-column")]
    [InlineData("a\tb\tc")]
    public void Parse_line_returns_null_for_anything_unusable(string line)
    {
        ChecksumListFile.ParseLine(line).Should().BeNull();
    }

    [Fact]
    public void Parse_line_returns_null_for_null_and_for_an_empty_path()
    {
        ChecksumListFile.ParseLine(null).Should().BeNull();
        ChecksumListFile.ParseLine(HashA + "\t   ").Should().BeNull();
    }

    [Fact]
    public void Read_of_a_missing_file_returns_an_empty_list()
    {
        using var dir = new TempDir();

        ChecksumListFile.Read(Path.Combine(dir.Path, "nothing-here.txt")).Should().BeEmpty();
    }

    [Fact]
    public void Write_then_read_round_trips_a_set_of_entries()
    {
        using var dir = new TempDir();
        var file = ChecksumListFile.PathIn(dir.Path);
        var entries = new[]
        {
            new ChecksumEntry(HashA, 10, Stamp(), @"C:\z\last.txt"),
            new ChecksumEntry(HashB, 20, Stamp(99), @"C:\a\first.txt"),
        };

        ChecksumListFile.Write(file, dir.Path, entries);
        var loaded = ChecksumListFile.Read(file);

        loaded.Should().HaveCount(2);
        loaded.Select(e => e.FullPath).Should().Equal(@"C:\a\first.txt", @"C:\z\last.txt");
        loaded[0].Md5.Should().Be(HashB);
        loaded[0].Length.Should().Be(20);
        loaded[0].ModifiedUtc.Should().Be(Stamp(99));
    }

    [Fact]
    public void Write_sorts_by_path_and_leads_with_comment_header_lines()
    {
        using var dir = new TempDir();
        var file = ChecksumListFile.PathIn(dir.Path);

        ChecksumListFile.Write(file, dir.Path, new[]
        {
            new ChecksumEntry(HashA, 1, Stamp(), @"C:\m\middle.txt"),
            new ChecksumEntry(HashA, 1, Stamp(), @"C:\a\aaa.txt"),
            new ChecksumEntry(HashB, 1, Stamp(), @"C:\z\zzz.txt"),
        });

        var lines = File.ReadAllLines(file);
        lines[0].Should().StartWith("#");
        lines.Count(l => l.StartsWith('#')).Should().BeGreaterThan(1);
        var data = lines.Where(l => !l.StartsWith('#')).ToList();
        data.Should().HaveCount(3);
        data[0].Should().EndWith(@"C:\a\aaa.txt");
        data[1].Should().EndWith(@"C:\m\middle.txt");
        data[2].Should().EndWith(@"C:\z\zzz.txt");
    }

    [Fact]
    public void Write_leaves_no_temporary_file_behind()
    {
        using var dir = new TempDir();
        var file = ChecksumListFile.PathIn(dir.Path);

        ChecksumListFile.Write(file, dir.Path, new[] { new ChecksumEntry(HashA, 1, Stamp(), @"C:\a.txt") });

        File.Exists(file).Should().BeTrue();
        File.Exists(file + ".tmp").Should().BeFalse();
    }

    [Theory]
    [InlineData(@"C:\pics\dedupe_md5_list.txt", true)]
    [InlineData(@"C:\pics\DEDUPE_MD5_LIST.TXT", true)]
    [InlineData(@"C:\pics\DeDupe_Md5_List.txt", true)]
    [InlineData(@"C:\pics\dedupe_md5_list.txt.tmp", true)]
    [InlineData(@"C:\pics\holiday.jpg", false)]
    [InlineData(@"C:\pics\md5_list.txt", false)]
    [InlineData(@"C:\pics\my_dedupe_md5_list.txt", false)]
    public void Is_list_file_recognises_the_list_and_its_temp_sibling(string path, bool expected)
    {
        ChecksumListFile.IsListFile(path).Should().Be(expected);
    }

    [Fact]
    public void Path_in_combines_the_root_with_the_fixed_file_name()
    {
        ChecksumListFile.PathIn(@"C:\pics").Should().Be(Path.Combine(@"C:\pics", ChecksumListFile.FileName));
        ChecksumListFile.FileName.Should().Be("dedupe_md5_list.txt");
    }

    [Fact]
    public void Read_skips_comments_blanks_and_malformed_lines()
    {
        using var dir = new TempDir();
        var file = Path.Combine(dir.Path, "hand-written.txt");
        File.WriteAllLines(file,
        [
            "# header",
            "",
            "   ",
            "rubbish",
            HashA + "\t5\t2026-01-02T03:04:05.0000000Z\t" + @"C:\ok.txt",
            "zzz\t5\t2026-01-02T03:04:05.0000000Z\t" + @"C:\bad-hash.txt",
        ]);

        var loaded = ChecksumListFile.Read(file);

        loaded.Should().HaveCount(1);
        loaded[0].FullPath.Should().Be(@"C:\ok.txt");
    }
}
