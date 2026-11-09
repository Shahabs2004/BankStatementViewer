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
    public double DisplayFontSize { get; set; } = 11;
    public string PrintFontFamilyName { get; set; } = "Tahoma";
    public double PrintFontSize { get; set; } = 8.3;
    public bool AutoFitColumns { get; set; } = true;
    public bool HideFeeRows { get; set; }
    public string RowHeightOption { get; set; } = "خودکار";
    public string ProvinceName { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string PrintBranchCode { get; set; } = "";
    public bool ShowVerticalGridLines { get; set; } = true;
    public bool ShowHorizontalGridLines { get; set; } = true;
}
