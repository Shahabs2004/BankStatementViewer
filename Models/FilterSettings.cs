using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BankStatementViewer.Models;

public sealed class FilterSettings : INotifyPropertyChanged
{
    private bool _typeFilterEnabled;
    private string _typeFilterMode = "واریز";
    private bool _branchFilterEnabled;
    private string _branchCode = string.Empty;
    private bool _trackingFilterEnabled;
    private string _trackingText = string.Empty;
    private bool _dateFilterEnabled;
    private string _dateFrom = string.Empty;
    private string _dateTo = string.Empty;
    private bool _rowNumberFilterEnabled;
    private int _rowFrom = 1;
    private int _rowTo = 999999;
    private bool _depositAmountFilterEnabled;
    private string _depositAmountMode = "Range";
    private decimal _depositAmountFrom;
    private decimal _depositAmountTo;
    private bool _withdrawalAmountFilterEnabled;
    private string _withdrawalAmountMode = "Range";
    private decimal _withdrawalAmountFrom;
    private decimal _withdrawalAmountTo;
    private bool _descriptionFilterEnabled;
    private string _descriptionSearchText = string.Empty;
    private bool _textSearchEnabled;
    private string _textSearchText = string.Empty;

    public bool TypeFilterEnabled { get => _typeFilterEnabled; set => SetField(ref _typeFilterEnabled, value); }
    public string TypeFilterMode { get => _typeFilterMode; set => SetField(ref _typeFilterMode, value); }
    public bool BranchFilterEnabled { get => _branchFilterEnabled; set => SetField(ref _branchFilterEnabled, value); }
    public string BranchCode { get => _branchCode; set => SetField(ref _branchCode, value); }
    public bool TrackingFilterEnabled { get => _trackingFilterEnabled; set => SetField(ref _trackingFilterEnabled, value); }
    public string TrackingText { get => _trackingText; set => SetField(ref _trackingText, value); }
    public bool DateFilterEnabled { get => _dateFilterEnabled; set => SetField(ref _dateFilterEnabled, value); }
    public string DateFrom { get => _dateFrom; set => SetField(ref _dateFrom, value); }
    public string DateTo { get => _dateTo; set => SetField(ref _dateTo, value); }
    public bool RowNumberFilterEnabled { get => _rowNumberFilterEnabled; set => SetField(ref _rowNumberFilterEnabled, value); }
    public int RowFrom { get => _rowFrom; set => SetField(ref _rowFrom, value); }
    public int RowTo { get => _rowTo; set => SetField(ref _rowTo, value); }
    public bool DepositAmountFilterEnabled { get => _depositAmountFilterEnabled; set => SetField(ref _depositAmountFilterEnabled, value); }
    public string DepositAmountMode { get => _depositAmountMode; set => SetField(ref _depositAmountMode, value); }
    public decimal DepositAmountFrom { get => _depositAmountFrom; set => SetField(ref _depositAmountFrom, value); }
    public decimal DepositAmountTo { get => _depositAmountTo; set => SetField(ref _depositAmountTo, value); }
    public bool WithdrawalAmountFilterEnabled { get => _withdrawalAmountFilterEnabled; set => SetField(ref _withdrawalAmountFilterEnabled, value); }
    public string WithdrawalAmountMode { get => _withdrawalAmountMode; set => SetField(ref _withdrawalAmountMode, value); }
    public decimal WithdrawalAmountFrom { get => _withdrawalAmountFrom; set => SetField(ref _withdrawalAmountFrom, value); }
    public decimal WithdrawalAmountTo { get => _withdrawalAmountTo; set => SetField(ref _withdrawalAmountTo, value); }
    public bool DescriptionFilterEnabled { get => _descriptionFilterEnabled; set => SetField(ref _descriptionFilterEnabled, value); }
    public string DescriptionSearchText { get => _descriptionSearchText; set => SetField(ref _descriptionSearchText, value); }
    public bool TextSearchEnabled { get => _textSearchEnabled; set => SetField(ref _textSearchEnabled, value); }
    public string TextSearchText { get => _textSearchText; set => SetField(ref _textSearchText, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
