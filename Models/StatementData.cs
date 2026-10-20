namespace BankStatementViewer.Models;

public sealed class StatementData
{
    public StatementHeader Header { get; init; } = new();
    public List<StatementTransaction> Transactions { get; init; } = new();
}
