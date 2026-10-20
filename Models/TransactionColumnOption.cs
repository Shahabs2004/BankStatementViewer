using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BankStatementViewer.Models;

public sealed class TransactionColumnOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public TransactionColumnOption(string key, string header, bool isSelected)
    {
        Key = key;
        Header = header;
        _isSelected = isSelected;
    }

    public string Key { get; }
    public string Header { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
