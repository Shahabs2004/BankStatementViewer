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
    [Fact]
    public void ApplyFullSettings_UpdatesDisplayFontAndPersistsAllDisplayPreferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"BankStatementViewer-{Guid.NewGuid():N}");
        Exception? testException = null;

        try
        {
            Directory.CreateDirectory(directory);
            var thread = new Thread(() =>
            {
                try
                {
                    var vm = new BankStatementViewer.ViewModels.MainViewModel(new SettingsService(directory));
                    var settings = new AppSettings
                    {
                        FontFamilyName = "B Nazanin",
                        DisplayFontSize = 16,
                        AutoFitColumns = false,
                        HideFeeRows = true,
                        RowHeightOption = "زیاد"
                    };

                    vm.ApplyFullSettings(settings);

                    var expectedFont = BankStatementViewer.ViewModels.MainViewModel.AvailableFontFamilies.Contains("B Nazanin") ? "B Nazanin" : "Tahoma";
                    Assert.Equal(expectedFont, vm.FontFamilyName);
                    Assert.Equal(16, vm.DisplayFontSize);
                    Assert.False(vm.AutoFitColumns);
                    Assert.True(vm.HideFeeRows);
                    Assert.Equal("زیاد", vm.RowHeightOption);

                    var saved = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
                        File.ReadAllText(vm.SettingsPath));
                    Assert.NotNull(saved);
                    Assert.Equal(expectedFont, saved.FontFamilyName);
                    Assert.Equal(16, saved.DisplayFontSize);
                    Assert.False(saved.AutoFitColumns);
                    Assert.True(saved.HideFeeRows);
                    Assert.Equal("زیاد", saved.RowHeightOption);
                }
                catch (Exception ex)
                {
                    testException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (testException is not null)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(testException).Throw();
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_BackupsCorruptSettingsAndReturnsWarning()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"BankStatementViewer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "appsettings.json");
        try
        {
            File.WriteAllText(path, "{ invalid json");
            var service = new SettingsService(directory);
            var loaded = service.Load(out var warning);
            Assert.NotNull(loaded);
            Assert.False(string.IsNullOrWhiteSpace(warning));
            Assert.Single(Directory.GetFiles(directory, "appsettings.json.corrupt-*"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

}
