using BankStatementViewer.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BankStatementViewer.Services;

public sealed class TransactionColumnDefinition
{
    public required string Key { get; init; }
    public required string Header { get; init; }
    public required Func<StatementTransaction, object?> Value { get; init; }
    public double PrintWeight { get; init; }
    public double MinimumPrintWidth { get; init; }
    public double ExcelWidth { get; init; }
    public bool DefaultForPrint { get; init; }
    public bool DefaultForExport { get; init; } = true;
    public bool DefaultForDisplay { get; init; } = true;
    public bool IsAmount { get; init; }
    public bool IsText { get; init; }

    public string DisplayText(StatementTransaction transaction)
    {
        var value = Value(transaction);
        if (IsAmount && value is decimal amount)
            return amount == 0 ? "—" : amount.ToString("N0", CultureInfo.InvariantCulture);
        if (value is decimal decimalValue)
            return decimalValue.ToString("N0", CultureInfo.InvariantCulture);
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static string MakeBreakable(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        // Keep full identifiers while letting WPF wrap long unspaced sequences.
        return Regex.Replace(value, @"(\S{14})(?=\S)", "$1\u200B");
    }
}

public static class TransactionColumnCatalog
{
    public static IReadOnlyList<TransactionColumnDefinition> All { get; } = new List<TransactionColumnDefinition>
    {
        new() { Key = "RowNumber", Header = "ردیف", Value = t => t.RowNumber, PrintWeight = 0.65, MinimumPrintWidth = 28, ExcelWidth = 8, DefaultForPrint = true },
        new() { Key = "Date", Header = "تاریخ", Value = t => t.Date, PrintWeight = 1.0, MinimumPrintWidth = 48, ExcelWidth = 13, DefaultForPrint = true },
        new() { Key = "Time", Header = "زمان", Value = t => t.Time, PrintWeight = 0.75, MinimumPrintWidth = 38, ExcelWidth = 10, DefaultForPrint = true },
        new() { Key = "TransactionType", Header = "نوع تراکنش", Value = t => t.TransactionType, PrintWeight = 0.85, MinimumPrintWidth = 42, ExcelWidth = 12 },
        new() { Key = "EntryKind", Header = "نوع ردیف", Value = t => t.EntryKind, PrintWeight = 0.85, MinimumPrintWidth = 48, ExcelWidth = 12, DefaultForPrint = true },
        new() { Key = "Iban", Header = "شبا", Value = t => t.Iban, PrintWeight = 1.3, MinimumPrintWidth = 78, ExcelWidth = 25, DefaultForPrint = false, DefaultForExport = false, DefaultForDisplay = false },
        new() { Key = "Deposit", Header = "واریز", Value = t => t.Deposit, PrintWeight = 1.0, MinimumPrintWidth = 56, ExcelWidth = 16, DefaultForPrint = true, IsAmount = true },
        new() { Key = "Withdrawal", Header = "برداشت", Value = t => t.Withdrawal, PrintWeight = 1.0, MinimumPrintWidth = 58, ExcelWidth = 16, DefaultForPrint = true, IsAmount = true },
        new() { Key = "Balance", Header = "مانده", Value = t => t.Balance, PrintWeight = 1.15, MinimumPrintWidth = 62, ExcelWidth = 17, DefaultForPrint = true, IsAmount = true },
        new() { Key = "BranchCode", Header = "شعبه", Value = t => t.BranchCode, PrintWeight = 0.75, MinimumPrintWidth = 42, ExcelWidth = 12, DefaultForPrint = true },
        new() { Key = "Tracking", Header = "پیگیری", Value = t => t.Tracking, PrintWeight = 0.95, MinimumPrintWidth = 58, ExcelWidth = 18, DefaultForPrint = true },
        new() { Key = "Identifier", Header = "شناسه", Value = t => t.Identifier, PrintWeight = 1.1, MinimumPrintWidth = 65, ExcelWidth = 20, DefaultForPrint = false, DefaultForExport = false, DefaultForDisplay = false },
        new() { Key = "TrackingDeposit", Header = "پیگیری واریز", Value = t => t.TrackingDeposit, PrintWeight = 1.1, MinimumPrintWidth = 66, ExcelWidth = 20, DefaultForExport = false, DefaultForDisplay = false },
        new() { Key = "Counterparty", Header = "واریزکننده/دریافت‌کننده", Value = t => t.Counterparty, PrintWeight = 1.7, MinimumPrintWidth = 100, ExcelWidth = 30, IsText = true, DefaultForPrint = false, DefaultForExport = false, DefaultForDisplay = false },
        new() { Key = "Channel", Header = "کانال", Value = t => t.Channel, PrintWeight = 0.75, MinimumPrintWidth = 42, ExcelWidth = 12, DefaultForPrint = true },
        new() { Key = "OtherAccount", Header = "حساب مقابل", Value = t => t.OtherAccount, PrintWeight = 1.4, MinimumPrintWidth = 74, ExcelWidth = 26, IsText = true },
        new() { Key = "Purpose", Header = "بابت / هدف", Value = t => t.Purpose, PrintWeight = 1.3, MinimumPrintWidth = 65, ExcelWidth = 28, IsText = true },
        new() { Key = "FullDescription", Header = "شرح کامل بانک", Value = t => t.RawDescription, PrintWeight = 2.6, MinimumPrintWidth = 100, ExcelWidth = 44, IsText = true, DefaultForPrint = false, DefaultForExport = false, DefaultForDisplay = false },
        new() { Key = "Description", Header = "توضیحات", Value = t => t.Description, PrintWeight = 3.2, MinimumPrintWidth = 130, ExcelWidth = 48, DefaultForPrint = true, IsText = true }
    };

    public static TransactionColumnDefinition Get(string key) =>
        All.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal))
        ?? throw new ArgumentException($"ستون ناشناخته است: {key}", nameof(key));

    public static IReadOnlyList<TransactionColumnDefinition> Resolve(IEnumerable<string> keys)
    {
        var selected = new HashSet<string>(keys ?? Array.Empty<string>(), StringComparer.Ordinal);
        var result = All.Where(c => selected.Contains(c.Key)).ToList();
        if (result.Count == 0) throw new InvalidOperationException("حداقل یک ستون برای خروجی انتخاب کنید.");
        return result;
    }
}
