using DigitalSecretary.Features.ClipboardHistory;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class ClipboardPreviewTests
{
    [Fact]
    public void Short_text_is_unchanged()
        => ClipboardPreview.Format("hello world").Should().Be("hello world");

    [Fact]
    public void Newlines_are_collapsed_to_spaces()
        => ClipboardPreview.Format("line1\r\nline2\nline3").Should().Be("line1 line2 line3");

    [Fact]
    public void Surrounding_whitespace_is_trimmed()
        => ClipboardPreview.Format("   padded   ").Should().Be("padded");

    [Fact]
    public void Long_text_is_truncated_with_ellipsis()
    {
        var input = new string('x', ClipboardPreview.MaxLength + 25);

        var result = ClipboardPreview.Format(input);

        result.Should().HaveLength(ClipboardPreview.MaxLength + 1);
        result.Should().EndWith("…");
        result.Should().StartWith(new string('x', ClipboardPreview.MaxLength));
    }

    [Fact]
    public void Text_at_max_length_is_not_truncated()
    {
        var input = new string('y', ClipboardPreview.MaxLength);

        ClipboardPreview.Format(input).Should().Be(input);
    }
}
