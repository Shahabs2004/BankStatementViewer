using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BankStatementViewer.Models;

public sealed class StatementTransaction : INotifyPropertyChanged
{
    private int _rowNumber;
    private string _date = string.Empty;
    private string _time = string.Empty;
    private string _iban = string.Empty;
    private decimal _deposit;
    private decimal _withdrawal;
    private decimal _balance;
    private string _branchCode = string.Empty;
    private string _tracking = string.Empty;
    private string _identifier = string.Empty;
    private string _trackingDeposit = string.Empty;
    private string _counterparty = string.Empty;
    private string _description = string.Empty;
    private string _rawDescription = string.Empty;
    private string _channel = "سایر";
    private string _otherAccount = string.Empty;
    private string _purpose = string.Empty;
    private bool _isFee;

    public int RowNumber { get => _rowNumber; set => SetField(ref _rowNumber, value); }
    public string Date { get => _date; set => SetField(ref _date, value); }
    public string Time { get => _time; set => SetField(ref _time, value); }
    public string Iban { get => _iban; set => SetField(ref _iban, value); }
    public decimal Deposit { get => _deposit; set { if (SetField(ref _deposit, value)) { OnPropertyChanged(nameof(TransactionType)); OnPropertyChanged(nameof(IsDeposit)); OnPropertyChanged(nameof(IsWithdrawal)); } } }
    public decimal Withdrawal { get => _withdrawal; set { if (SetField(ref _withdrawal, value)) { OnPropertyChanged(nameof(TransactionType)); OnPropertyChanged(nameof(IsDeposit)); OnPropertyChanged(nameof(IsWithdrawal)); } } }
    public decimal Balance { get => _balance; set => SetField(ref _balance, value); }
    public string BranchCode { get => _branchCode; set => SetField(ref _branchCode, value); }
    public string Tracking { get => _tracking; set => SetField(ref _tracking, value); }
    public string Identifier { get => _identifier; set => SetField(ref _identifier, value); }
    public string TrackingDeposit { get => _trackingDeposit; set => SetField(ref _trackingDeposit, value); }
    public string Counterparty { get => _counterparty; set => SetField(ref _counterparty, value); }
    public string Description { get => _description; set => SetField(ref _description, value); }
    public string RawDescription { get => _rawDescription; set => SetField(ref _rawDescription, value); }
    public string Channel { get => _channel; set => SetField(ref _channel, value); }
    public string OtherAccount { get => _otherAccount; set => SetField(ref _otherAccount, value); }
    public string Purpose { get => _purpose; set => SetField(ref _purpose, value); }
    public bool IsFee { get => _isFee; set { if (SetField(ref _isFee, value)) { OnPropertyChanged(nameof(FeeAmount)); OnPropertyChanged(nameof(EntryKind)); } } }
    public string EntryKind => IsFee ? "کارمزد" : "تراکنش";
    public decimal FeeAmount => IsFee ? Math.Abs(Deposit) + Math.Abs(Withdrawal) : 0m;
    public decimal TransactionAmount => Math.Abs(Deposit) + Math.Abs(Withdrawal);

    public string TransactionType => Deposit > 0 ? "واریز" : Withdrawal > 0 ? "برداشت" : "—";
    public bool IsDeposit => Deposit > 0;
    public bool IsWithdrawal => Withdrawal > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
