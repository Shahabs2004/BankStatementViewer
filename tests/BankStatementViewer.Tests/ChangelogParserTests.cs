using BankStatementViewer.Services;
using Xunit;

namespace BankStatementViewer.Tests;

public class ChangelogParserTests
{
    [Fact]
    public void Parse_ReadsVersionHeadersDatesAndBulletItems()
    {
        const string markdown = """
            # تاریخچه تغییرات

            Some intro paragraph that must be ignored.

            ## 1.1.19 (2026-11-11)
            - اصلاح پارسر
            - افزودن CI

            ## 1.1.18 (2026-11-11)
            - بازطراحی ظاهر
            """;

        var entries = ChangelogParser.Parse(markdown, currentVersion: null);

        Assert.Equal(2, entries.Count);
        Assert.Equal("1.1.19", entries[0].Version);
        Assert.Equal("2026-11-11", entries[0].Date);
        Assert.Equal(new[] { "اصلاح پارسر", "افزودن CI" }, entries[0].Items);
        Assert.Equal("1.1.18", entries[1].Version);
        Assert.Equal(new[] { "بازطراحی ظاهر" }, entries[1].Items);
    }

    [Fact]
    public void Parse_MarksCurrentVersionIgnoringVCase()
    {
        const string markdown = """
            ## v1.1.10 (2026-11-09)
            - تست دهدهی

            ## 1.1.9 (2026-11-09)
            - قبلی
            """;

        var entries = ChangelogParser.Parse(markdown, currentVersion: "1.1.10");

        Assert.True(entries[0].IsCurrent);
        Assert.False(entries[1].IsCurrent);
    }

    [Fact]
    public void Parse_HandlesHeaderWithoutDateAndFileWithoutTrailingItems()
    {
        const string markdown = "## 2.0\n## 1.0 (2026-01-01)\n- تنها مورد\n";

        var entries = ChangelogParser.Parse(markdown, currentVersion: "2.0");

        Assert.Equal(2, entries.Count);
        Assert.Equal("2.0", entries[0].Version);
        Assert.Equal("", entries[0].Date);
        Assert.Empty(entries[0].Items);
        Assert.Equal(new[] { "تنها مورد" }, entries[1].Items);
    }
}
