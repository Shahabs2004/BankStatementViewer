namespace BankStatementViewer.Models;

public sealed class AppSettings
{
    public List<SearchPreset> SearchPresets { get; set; } = new();
    public FilterSettings LastFilters { get; set; } = new();
    public List<string> DisplayColumns { get; set; } = new()
    {
        "RowNumber", "Date", "Time", "TransactionType", "EntryKind", "Channel", "Deposit", "Withdrawal",
        "Balance", "BranchCode", "Tracking", "OtherAccount", "Purpose", "Description"
    };
    public string FontFamilyName { get; set; } = "Tahoma";
    public bool AutoFitColumns { get; set; } = true;
    public bool HideFeeRows { get; set; }
    public string RowHeightOption { get; set; } = "خودکار";
}
