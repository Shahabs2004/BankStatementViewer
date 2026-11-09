using System.Windows;
using System.Collections.ObjectModel;
using BankStatementViewer.Models;
using BankStatementViewer.Services;

namespace BankStatementViewer.Views;

public partial class SettingsWindow : Window
{
    private readonly FtpStatementService _ftpService = new();
    public AppSettings Settings { get; private set; }
    public ObservableCollection<TransactionColumnOption> DisplayColumns { get; } = new();
    public IReadOnlyList<string> Fonts => BankStatementViewer.ViewModels.MainViewModel.AvailableFontFamilies;
    public IReadOnlyList<string> RowHeights { get; } = new[] { "خودکار", "کم", "متوسط", "زیاد" };
    public IReadOnlyList<double> DisplayFontSizes { get; } = Enumerable.Range(9, 10).Select(x => (double)x).ToArray();
    public IReadOnlyList<double> PrintFontSizes { get; } = Enumerable.Range(7, 8).Select(x => (double)x).ToArray();

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Settings = settings;
        var selectedColumns = new HashSet<string>(settings.DisplayColumns ?? new List<string>(), StringComparer.Ordinal);
        foreach (var column in TransactionColumnCatalog.All)
            DisplayColumns.Add(new TransactionColumnOption(column.Key, column.Header, selectedColumns.Contains(column.Key)));
        DataContext = this;
        ProvinceBox.Text = settings.ProvinceName;
        BranchBox.Text = settings.BranchName;
        BranchCodeBox.Text = settings.PrintBranchCode;
        VerticalLinesCheck.IsChecked = settings.ShowVerticalGridLines;
        HorizontalLinesCheck.IsChecked = settings.ShowHorizontalGridLines;
        AutoFitCheck.IsChecked = settings.AutoFitColumns;
        HideFeesCheck.IsChecked = settings.HideFeeRows;
        FontCombo.SelectedItem = settings.FontFamilyName;
        DisplayFontSizeCombo.SelectedItem = settings.DisplayFontSize;
        PrintFontCombo.SelectedItem = settings.PrintFontFamilyName;
        PrintFontSizeCombo.SelectedItem = settings.PrintFontSize;
        RowHeightCombo.SelectedItem = settings.RowHeightOption;
    }

    private void Ftp_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FtpSettingsWindow(_ftpService.LoadSettings()) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            try { _ftpService.SaveSettings(dialog.Settings); MessageBox.Show(this, "تنظیمات FTP ذخیره شد."); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var selectedColumns = DisplayColumns.Where(column => column.IsSelected).Select(column => column.Key).ToList();
        if (selectedColumns.Count == 0)
        {
            MessageBox.Show(this, "حداقل یک ستون برای نمایش در جدول اصلی انتخاب کنید.", "تنظیم ستون‌ها", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Settings.DisplayColumns = selectedColumns;
        Settings.ProvinceName = ProvinceBox.Text.Trim();
        Settings.BranchName = BranchBox.Text.Trim();
        Settings.PrintBranchCode = BranchCodeBox.Text.Trim();
        Settings.ShowVerticalGridLines = VerticalLinesCheck.IsChecked == true;
        Settings.ShowHorizontalGridLines = HorizontalLinesCheck.IsChecked == true;
        Settings.AutoFitColumns = AutoFitCheck.IsChecked == true;
        Settings.HideFeeRows = HideFeesCheck.IsChecked == true;
        Settings.FontFamilyName = FontCombo.SelectedItem as string ?? "Tahoma";
        Settings.DisplayFontSize = DisplayFontSizeCombo.SelectedItem is double displaySize ? displaySize : 11;
        Settings.PrintFontFamilyName = PrintFontCombo.SelectedItem as string ?? "Tahoma";
        Settings.PrintFontSize = PrintFontSizeCombo.SelectedItem is double printSize ? printSize : 8.3;
        Settings.RowHeightOption = RowHeightCombo.SelectedItem as string ?? "خودکار";
        DialogResult = true;
    }
}