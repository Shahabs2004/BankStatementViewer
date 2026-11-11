using System.Globalization;
using BankStatementViewer.Models;
using BankStatementViewer.Services;
using ClosedXML.Excel;
using Xunit;
using Xunit.Abstractions;

namespace BankStatementViewer.Tests;

public sealed class ExcelExportServiceTests
{
    private readonly ITestOutputHelper _output;

    public ExcelExportServiceTests(ITestOutputHelper output) => _output = output;
    [Fact]
    public void Export_PreservesTrackingAndIdentifierAsText()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        try
        {
            var transaction = new StatementTransaction
            {
                Tracking = "00123",
                Identifier = "1234567890123456",
                Date = "1405/01/01",
                Description = "test"
            };
            new ExcelExportService().Export(path, new StatementHeader(),
                new[] { transaction }, new[] { "Tracking", "Identifier" });
            using var workbook = new XLWorkbook(path);
            var sheet = workbook.Worksheet(1);
            Assert.Equal("00123", sheet.Cell(8, 1).GetString());
            Assert.Equal("1234567890123456", sheet.Cell(8, 2).GetString());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void FaIrCulture_ExposesCalendarAndNumberGroupSeparator()
    {
        var culture = CultureInfo.GetCultureInfo("fa-IR");
        _output.WriteLine($"Calendar: {culture.DateTimeFormat.Calendar.GetType().FullName}");
        _output.WriteLine($"NumberGroupSeparator: '{culture.NumberFormat.NumberGroupSeparator}'");
        Assert.NotNull(culture.DateTimeFormat.Calendar);
        Assert.False(string.IsNullOrEmpty(culture.NumberFormat.NumberGroupSeparator));
    }
}
