namespace BankStatementViewer.Models;

public sealed class SearchPreset
{
    public string Name { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;

    public override string ToString() => Name;
}
