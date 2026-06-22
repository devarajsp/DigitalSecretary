using DigitalSecretary.Features.EmailIntelligence;
using FluentAssertions;
using Xunit;

namespace DigitalSecretary.UnitTests.Features;

public sealed class ArchiveScannerTests
{
    [Fact]
    public void Filter_prefers_eml_over_its_txt_sibling()
    {
        var input = new[]
        {
            @"C:\a\x.txt", @"C:\a\x.eml", @"C:\a\y.txt", @"C:\a\z.eml",
        };

        var result = ArchiveScanner.Filter(input);

        result.Should().Contain(@"C:\a\x.eml");
        result.Should().NotContain(@"C:\a\x.txt");
        result.Should().Contain(@"C:\a\y.txt");
        result.Should().Contain(@"C:\a\z.eml");
    }
}
