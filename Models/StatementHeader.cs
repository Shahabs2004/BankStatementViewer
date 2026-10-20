namespace BankStatementViewer.Models;

public sealed class StatementHeader
{
    public string CustomerName { get; set; } = string.Empty;
    public string Iban { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string ReportFrom { get; set; } = string.Empty;
    public string ReportTo { get; set; } = string.Empty;
    public string ReceivedDate { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }

    public string ReportPeriod =>
        string.IsNullOrWhiteSpace(ReportFrom) && string.IsNullOrWhiteSpace(ReportTo)
            ? "—"
            : $"{(string.IsNullOrWhiteSpace(ReportFrom) ? "—" : ReportFrom)} تا {(string.IsNullOrWhiteSpace(ReportTo) ? "—" : ReportTo)}";
}
