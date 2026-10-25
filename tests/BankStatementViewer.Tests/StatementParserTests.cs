using BankStatementViewer.Models;
using BankStatementViewer.Services;
using ClosedXML.Excel;
using Xunit;

namespace BankStatementViewer.Tests;

public sealed class StatementParserTests
{
    private const int ExpectedTransactionCount = 128;
    private static readonly StatementParser Parser = new();

    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "sample-statement.xls");

    [Fact]
    public void Parse_ReadsHeaderFieldsByLabels()
    {
        var result = Parser.Parse(FixturePath);

        Assert.Equal("1405/07/17", result.Header.ReportFrom);
        Assert.Equal("1405/07/18", result.Header.ReportTo);
        Assert.Equal("1962", result.Header.BranchCode);
        Assert.Equal("IR00000000000000000000000000", result.Header.Iban);
        Assert.Equal("1405/07/21", result.Header.ReceivedDate);
    }

    [Fact]
    public void Parse_ReturnsExpectedTransactionCountAndSkipsTotalsAndFooter()
    {
        var result = Parser.Parse(FixturePath);

        Assert.Equal(ExpectedTransactionCount, result.Transactions.Count);
        Assert.All(result.Transactions, transaction =>
            Assert.False(string.IsNullOrWhiteSpace(transaction.Date)));
        Assert.DoesNotContain(result.Transactions, transaction =>
            transaction.RawDescription.Contains("مهر و امضاء مجاز", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Transactions, transaction =>
            transaction.RawDescription.Contains("جمع کل", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_CalculatesOpeningBalanceFromFirstTransactionWhenPriorBalanceIsEmpty()
    {
        var result = Parser.Parse(FixturePath);
        var first = result.Transactions[0];

        var expected = first.Balance - first.Deposit + first.Withdrawal;

        Assert.Equal(expected, result.Header.OpeningBalance);
    }

    [Fact]
    public void Parse_MarksFeeRowsButNotTransferRows()
    {
        var result = Parser.Parse(FixturePath);
        var feeRows = result.Transactions.Where(transaction => transaction.IsFee).ToList();
        // In this bank export, the fee row often repeats the same short transaction
        // label ("انتقالMOB" or "پايا"). The actual transfer row includes the
        // multi-line details, while the following fee row usually contains only the label.
        var transferRows = result.Transactions.Where(transaction =>
            (transaction.RawDescription.StartsWith("انتقالMOB", StringComparison.Ordinal) ||
             transaction.RawDescription.StartsWith("پايا", StringComparison.Ordinal)) &&
            transaction.RawDescription.Contains('\n')).ToList();

        Assert.NotEmpty(feeRows);
        Assert.NotEmpty(transferRows);
        Assert.All(feeRows, transaction => Assert.True(transaction.IsFee));
        Assert.All(transferRows, transaction => Assert.False(transaction.IsFee));
    }

    [Fact]
    public void Parse_RejectsNonExcelFileWithPersianMessage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.XLS");
        try
        {
            File.WriteAllText(path, "This is not an Excel workbook.");

            var exception = Assert.Throws<InvalidDataException>(() => Parser.Parse(path));

            Assert.Equal(StatementParser.InvalidExcelMessage, exception.Message);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Parse_ThrowsPersianErrorWhenDepositHeaderIsMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Statement");
                var headers = new[]
                {
                    "شبا", "توضیحات", "پیگیری واریز", "شناسه",
                    "واریز کننده/دریافت کننده", "مانده", "برداشت",
                    "شعبه", "پیگیری", "زمان", "تاریخ", "ردیف"
                };
                for (var i = 0; i < headers.Length; i++)
                    sheet.Cell(7, i + 1).Value = headers[i];
                sheet.Cell(8, 2).Value = "انتقالMOB";
                sheet.Cell(8, 6).Value = 1000;
                sheet.Cell(8, 11).Value = "1403/01/01";
                sheet.Cell(8, 12).Value = 1;
                workbook.SaveAs(path);
            }

            var exception = Assert.Throws<InvalidDataException>(() => Parser.Parse(path));

            Assert.Contains("واریز", exception.Message);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
