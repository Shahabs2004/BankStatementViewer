using System.ComponentModel;
using System.Collections.ObjectModel;
using BankStatementViewer.Models;
using BankStatementViewer.Services;
using System.Windows;

namespace BankStatementViewer.Views;

public partial class ColumnSelectionDialog : Window
{
    private readonly bool _forPrint;

    public ColumnSelectionDialog(bool forPrint, IReadOnlyCollection<string>? previousSelection = null, bool forDisplay = false)
    {
        InitializeComponent();
        _forPrint = forPrint;
        Title = forDisplay ? "انتخاب ستون‌های نمایش برنامه" : forPrint ? "انتخاب ستون‌های چاپ" : "انتخاب ستون‌های خروجی Excel";
        DescriptionText.Text = forDisplay
            ? "ستون‌هایی را که در جدول اصلی برنامه می‌خواهید ببینید انتخاب کنید. این انتخاب ذخیره می‌شود و در اجرای بعدی هم باقی می‌ماند."
            : forPrint
                ? "ستون‌های مورد نیاز برای چاپ را انتخاب کنید. برای خوانایی بهتر، در حالت عمودی تعداد ستون‌ها را کمتر نگه دارید. متن توضیحات در صورت نیاز چندخطی چاپ می‌شود."
                : "ستون‌هایی را که باید در فایل Excel درج شوند انتخاب کنید. اطلاعات حساب در سربرگ فایل باقی می‌ماند.";

        var hasPreviousSelection = previousSelection is { Count: > 0 };
        var selectedKeys = hasPreviousSelection
            ? new HashSet<string>(previousSelection!, StringComparer.Ordinal)
            : null;
        Options = new ObservableCollection<TransactionColumnOption>(TransactionColumnCatalog.All.Select(c =>
            new TransactionColumnOption(c.Key, c.Header,
                hasPreviousSelection ? selectedKeys!.Contains(c.Key) : (forDisplay ? c.DefaultForDisplay : forPrint ? c.DefaultForPrint : c.DefaultForExport))));
        foreach (var option in Options) option.PropertyChanged += Option_PropertyChanged;
        DataContext = this;
        UpdateSelectedCount();
    }

    public ObservableCollection<TransactionColumnOption> Options { get; }
    public IReadOnlyList<string> SelectedKeys => Options.Where(o => o.IsSelected).Select(o => o.Key).ToList();
    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var option in Options) option.IsSelected = true;
        UpdateSelectedCount();
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        foreach (var option in Options)
            option.IsSelected = _forPrint ? TransactionColumnCatalog.Get(option.Key).DefaultForPrint : TransactionColumnCatalog.Get(option.Key).DefaultForExport;
        UpdateSelectedCount();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!Options.Any(o => o.IsSelected))
        {
            MessageBox.Show(this, "حداقل یک ستون را انتخاب کنید.", "انتخاب ستون‌ها", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }

    private void Option_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TransactionColumnOption.IsSelected)) UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        var count = Options.Count(o => o.IsSelected);
        SelectedCountText.Text = $"انتخاب‌شده: {count} از {Options.Count}";
    }
}
