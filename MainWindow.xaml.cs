using System.Windows;
using System.IO;
using Microsoft.Win32;
using BankStatementViewer.Models;
using BankStatementViewer.Services;
using BankStatementViewer.ViewModels;
using BankStatementViewer.Views;

namespace BankStatementViewer;

public partial class MainWindow : Window
{
    private bool _startupFileDialogShown;
    private readonly ExcelExportService _excelExportService = new();
    private readonly PrintService _printService = new();
    private IReadOnlyList<string>? _lastPrintColumns;
    private IReadOnlyList<string>? _lastExcelColumns;

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow() => InitializeComponent();

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyGridColumnSettings();
        if (_startupFileDialogShown) return;
        _startupFileDialogShown = true;
        OpenStatementDialog();
        ApplyGridColumnSettings();
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e) => OpenStatementDialog();

    private void OpenStatementDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "انتخاب فایل صورت‌حساب بانکی",
            Filter = "فایل‌های صورت‌حساب (*.xls;*.xlsx;*.txt;*.tab;*.tsv)|*.xls;*.xlsx;*.txt;*.tab;*.tsv|همه فایل‌ها (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            ViewModel.LoadStatement(dialog.FileName);
            Title = $"نمایش‌دهنده گردش حساب — {Path.GetFileName(dialog.FileName)}";
            ApplyGridColumnSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "خطا در خواندن صورت‌حساب", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DisplayColumns_Click(object sender, RoutedEventArgs e)
    {
        var columnDialog = new ColumnSelectionDialog(forPrint: false, previousSelection: ViewModel.DisplayColumns, forDisplay: true)
        {
            Owner = this
        };
        if (columnDialog.ShowDialog() != true) return;

        try
        {
            ViewModel.SetDisplayColumns(columnDialog.SelectedKeys);
            ApplyGridColumnSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"ذخیره ستون‌های نمایش ناموفق بود:\n{ex.Message}", "تنظیم ستون‌ها", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AutoFitColumns_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.IsChecked is bool isChecked && ViewModel.AutoFitColumns != isChecked)
            ViewModel.AutoFitColumns = isChecked;
        ApplyGridColumnSettings();
    }

    private void RowHeightOption_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is System.Windows.Controls.ComboBox combo &&
            combo.SelectedItem is string selected && !string.Equals(vm.RowHeightOption, selected, StringComparison.Ordinal))
            vm.RowHeightOption = selected;
        ApplyRowHeightSettings();
    }

    private void ApplyRowHeightSettings()
    {
        if (TransactionsGrid is null || DataContext is not MainViewModel vm) return;

        // Keep RowHeight automatic so wrapped multiline descriptions are never clipped.
        // The setting controls only the minimum row height; users can also drag row borders.
        TransactionsGrid.RowHeight = double.NaN;
        TransactionsGrid.MinRowHeight = vm.RowHeightOption switch
        {
            "کم" => 30,
            "متوسط" => 46,
            "زیاد" => 68,
            _ => 24
        };
        TransactionsGrid.CanUserResizeRows = true;
    }

    private void ApplyGridColumnSettings()
    {
        if (TransactionsGrid is null || DataContext is not MainViewModel vm) return;
        ApplyRowHeightSettings();

        var visibleKeys = new HashSet<string>(vm.DisplayColumns, StringComparer.Ordinal);
        foreach (var column in TransactionsGrid.Columns)
        {
            var header = Convert.ToString(column.Header) ?? string.Empty;
            var definition = TransactionColumnCatalog.All.FirstOrDefault(c => string.Equals(c.Header, header, StringComparison.Ordinal));
            if (definition is null) continue;

            column.Visibility = visibleKeys.Contains(definition.Key)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            column.MinWidth = definition.Key switch
            {
                "RowNumber" => 42,
                "Date" => 72,
                "Time" => 52,
                _ => 58
            };

            if (vm.AutoFitColumns)
            {
                column.MaxWidth = definition.Key switch
                {
                    "RowNumber" => 85,
                    "Date" => 125,
                    "Time" => 95,
                    "TransactionType" => 115,
                    "EntryKind" => 115,
                    "Iban" => 205,
                    "Deposit" or "Withdrawal" or "Balance" => 170,
                    "BranchCode" => 110,
                    "Tracking" or "TrackingDeposit" => 190,
                    "Identifier" => 220,
                    "Counterparty" => 260,
                    "Channel" => 120,
                    "OtherAccount" => 360,
                    "Purpose" => 320,
                    "FullDescription" => 620,
                    "Description" => 620,
                    _ => 180
                };
                column.Width = definition.Key == "Description"
                    ? new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
                    : System.Windows.Controls.DataGridLength.Auto;
            }
            else
            {
                column.MaxWidth = double.PositiveInfinity;
                column.Width = definition.Key switch
                {
                    "RowNumber" => new System.Windows.Controls.DataGridLength(55),
                    "Date" => new System.Windows.Controls.DataGridLength(95),
                    "Time" => new System.Windows.Controls.DataGridLength(70),
                    "TransactionType" => new System.Windows.Controls.DataGridLength(72),
                    "EntryKind" => new System.Windows.Controls.DataGridLength(85),
                    "Iban" => new System.Windows.Controls.DataGridLength(150),
                    "Deposit" or "Withdrawal" => new System.Windows.Controls.DataGridLength(105),
                    "Balance" => new System.Windows.Controls.DataGridLength(115),
                    "BranchCode" => new System.Windows.Controls.DataGridLength(70),
                    "Tracking" => new System.Windows.Controls.DataGridLength(85),
                    "Identifier" or "TrackingDeposit" => new System.Windows.Controls.DataGridLength(110),
                    "Counterparty" => new System.Windows.Controls.DataGridLength(160),
                    "Channel" => new System.Windows.Controls.DataGridLength(80),
                    "OtherAccount" => new System.Windows.Controls.DataGridLength(180),
                    "Purpose" => new System.Windows.Controls.DataGridLength(160),
                    "FullDescription" => new System.Windows.Controls.DataGridLength(350),
                    "Description" => new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star),
                    _ => new System.Windows.Controls.DataGridLength(100)
                };
            }
        }
        TransactionsGrid.UpdateLayout();
    }

    private async void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Header is null)
        {
            MessageBox.Show(this, "ابتدا یک صورت‌حساب باز کنید.", "خروجی Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var columnDialog = new ColumnSelectionDialog(forPrint: false, previousSelection: _lastExcelColumns) { Owner = this };
        if (columnDialog.ShowDialog() != true) return;
        var selectedExcelColumns = columnDialog.SelectedKeys.ToList();
        _lastExcelColumns = selectedExcelColumns;

        var dialog = new SaveFileDialog
        {
            Title = "ذخیره خروجی Excel",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"گردش_حساب_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;

        var header = ViewModel.Header!;
        var transactions = ViewModel.FilteredTransactions.ToList();
        var progress = new Progress<int>(value => OperationProgressBar.Value = value);
        SetBusy(true, "در حال ساخت فایل Excel...", indeterminate: false);
        try
        {
            await Task.Run(() => _excelExportService.Export(dialog.FileName, header, transactions, selectedExcelColumns, progress));
            MessageBox.Show(this, $"فایل Excel با موفقیت ذخیره شد.\n{dialog.FileName}", "خروجی موفق", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"ساخت فایل Excel ناموفق بود:\n{ex.Message}", "خطای خروجی", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    private void PrintPortrait_Click(object sender, RoutedEventArgs e) => PrintStatement(landscape: false);
    private void PrintLandscape_Click(object sender, RoutedEventArgs e) => PrintStatement(landscape: true);

    private async void PrintStatement(bool landscape)
    {
        if (ViewModel.Header is null)
        {
            MessageBox.Show(this, "ابتدا یک صورت‌حساب باز کنید.", "چاپ", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var columnDialog = new ColumnSelectionDialog(forPrint: true, previousSelection: _lastPrintColumns) { Owner = this };
        if (columnDialog.ShowDialog() != true) return;
        var selectedPrintColumns = columnDialog.SelectedKeys.ToList();
        _lastPrintColumns = selectedPrintColumns;

        var header = ViewModel.Header!;
        var transactions = ViewModel.FilteredTransactions.ToList();
        SetBusy(true, "در حال آماده‌سازی چاپ...");
        try
        {
            await _printService.PrintAsync(header, transactions, landscape, selectedPrintColumns);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"چاپ ناموفق بود:\n{ex.Message}", "خطای چاپ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { SetBusy(false); }
    }

    private void QuickSummary_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Header is null)
        {
            MessageBox.Show(this, "ابتدا یک صورت‌حساب باز کنید.", "خلاصه سریع", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new QuickSummaryWindow(ViewModel.MatchedTransactions, ViewModel.FontFamilyName) { Owner = this };
        dialog.ShowDialog();
    }

    private void SetBusy(bool busy, string status = "", bool indeterminate = true)
    {
        OpenFileButton.IsEnabled = !busy;
        ExportExcelButton.IsEnabled = !busy;
        PrintPortraitButton.IsEnabled = !busy;
        PrintLandscapeButton.IsEnabled = !busy;
        OperationProgressBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        OperationProgressBar.IsIndeterminate = busy && indeterminate;
        if (busy) OperationProgressBar.Value = 0;
        OperationStatusText.Text = status;
        OperationStatusText.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.SaveSettings();
            MessageBox.Show(this, "تنظیمات برنامه، ستون‌های نمایش، فونت و فیلترها ذخیره شدند.", "ذخیره تنظیمات", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"ذخیره تنظیمات در پوشه AppData ناموفق بود.\n{ex.Message}", "خطای ذخیره تنظیمات", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearFilters();
        PresetCombo.SelectedItem = null;
    }

    private void PresetCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PresetCombo.SelectedItem is SearchPreset preset)
        {
            ViewModel.Filters.DescriptionSearchText = preset.SearchText;
            ViewModel.Filters.DescriptionFilterEnabled = true;
        }
    }

    private void AddPreset_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            ViewModel.AddPreset(dialog.PresetName, dialog.SearchText);
            PresetCombo.SelectedItem = ViewModel.Presets.LastOrDefault(p => p.Name == dialog.PresetName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "افزودن پیش‌فرض", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RemovePreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetCombo.SelectedItem is not SearchPreset preset)
        {
            MessageBox.Show(this, "ابتدا یک پیش‌فرض برای حذف انتخاب کنید.", "حذف پیش‌فرض", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(this, $"پیش‌فرض «{preset.Name}» حذف شود؟", "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            ViewModel.RemovePreset(preset);
    }
}
