using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BankStatementViewer.ViewModels;

public sealed class EqualityConverter : IValueConverter
{
    public static readonly EqualityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(System.Convert.ToString(value, culture), System.Convert.ToString(parameter, culture), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool isChecked && isChecked ? parameter?.ToString() ?? string.Empty : Binding.DoNothing;
}
