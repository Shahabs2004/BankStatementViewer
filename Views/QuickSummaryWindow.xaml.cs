using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using BankStatementViewer.Models;

namespace BankStatementViewer.Views;

public partial class QuickSummaryWindow : Window
{
    public QuickSummaryWindow(IReadOnlyCollection<StatementTransaction> transactions, string fontFamily)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(fontFamily)) FontFamily = new FontFamily(fontFamily);
        var rows = transactions.ToList();
        ChannelSummaries = new ObservableCollection<SummaryGroupRow>(rows
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Channel) ? "سایر" : t.Channel)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => BuildSummary(g.Key, g)));
        DailySummaries = new ObservableCollection<SummaryGroupRow>(rows
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Date) ? "بدون تاریخ" : t.Date)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => BuildSummary(g.Key, g)));
        LargeTransactions = new ObservableCollection<LargeTransactionRow>(rows.Where(t => !t.IsFee)
            .OrderByDescending(t => t.TransactionAmount)
            .Take(20)
            .Select(t => new LargeTransactionRow(t.RowNumber, t.Date, t.Time, t.TransactionType, t.Channel,
                t.TransactionAmount, t.Tracking, t.Description)));
        TotalFees = rows.Sum(t => t.FeeAmount);
        DataContext = this;
    }

    public ObservableCollection<SummaryGroupRow> ChannelSummaries { get; }
    public ObservableCollection<SummaryGroupRow> DailySummaries { get; }
    public ObservableCollection<LargeTransactionRow> LargeTransactions { get; }
    public decimal TotalFees { get; }

    private static SummaryGroupRow BuildSummary(string key, IEnumerable<StatementTransaction> group)
    {
        var all = group.ToList();
        var regular = all.Where(t => !t.IsFee).ToList();
        return new SummaryGroupRow(key, regular.Count, all.Count(t => t.IsFee),
            regular.Sum(t => t.Deposit), regular.Sum(t => t.Withdrawal), all.Sum(t => t.FeeAmount));
    }
}

public sealed record SummaryGroupRow(string Channel, int Count, int FeeCount, decimal Deposits, decimal Withdrawals, decimal Fees)
{
    public string Date => Channel;
}

public sealed record LargeTransactionRow(int RowNumber, string Date, string Time, string TransactionType,
    string Channel, decimal Amount, string Tracking, string Description);
