using BankStatementViewer.Models;
using BankStatementViewer.Services;
using Xunit;

namespace BankStatementViewer.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void SaveAndLoad_PreservesDisplayAndPrintFontPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"BankStatementViewer-{Guid.NewGuid():N}");
        try
        {
            var service = new SettingsService(directory);
            var expected = new AppSettings
            {
                FontFamilyName = "B Nazanin",
                DisplayFontSize = 16,
                PrintFontFamilyName = "B Mitra",
                PrintFontSize = 10
            };

            service.Save(expected);
            var actual = service.Load(out var warning);

            Assert.Null(warning);
            Assert.Equal(expected.FontFamilyName, actual.FontFamilyName);
            Assert.Equal(expected.DisplayFontSize, actual.DisplayFontSize);
            Assert.Equal(expected.PrintFontFamilyName, actual.PrintFontFamilyName);
            Assert.Equal(expected.PrintFontSize, actual.PrintFontSize);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
