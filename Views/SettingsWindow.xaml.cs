using System.Windows;
using BankStatementViewer.Models;
using BankStatementViewer.Services;

namespace BankStatementViewer.Views;

public partial class SettingsWindow : Window
{
    private readonly FtpStatementService _ftpService = new();
    public AppSettings Settings { get; private set; }
    public IReadOnlyList<string> Fonts => BankStatementViewer.ViewModels.MainViewModel.AvailableFontFamilies;
    public IReadOnlyList<string> RowHeights { get; } = new[] { "خودکار", "کم", "متوسط", "زیاد" };

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        Settings = settings;
        DataContext = this;
        ProvinceBox.Text = settings.ProvinceName;
        BranchBox.Text = settings.BranchName;
        BranchCodeBox.Text = settings.PrintBranchCode;
        VerticalLinesCheck.IsChecked = settings.ShowVerticalGridLines;
        HorizontalLinesCheck.IsChecked = settings.ShowHorizontalGridLines;
        AutoFitCheck.IsChecked = settings.AutoFitColumns;
        HideFeesCheck.IsChecked = settings.HideFeeRows;
        FontCombo.SelectedItem = settings.FontFamilyName;
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
        Settings.ProvinceName = ProvinceBox.Text.Trim();
        Settings.BranchName = BranchBox.Text.Trim();
        Settings.PrintBranchCode = BranchCodeBox.Text.Trim();
        Settings.ShowVerticalGridLines = VerticalLinesCheck.IsChecked == true;
        Settings.ShowHorizontalGridLines = HorizontalLinesCheck.IsChecked == true;
        Settings.AutoFitColumns = AutoFitCheck.IsChecked == true;
        Settings.HideFeeRows = HideFeesCheck.IsChecked == true;
        Settings.FontFamilyName = FontCombo.SelectedItem as string ?? "Tahoma";
        Settings.RowHeightOption = RowHeightCombo.SelectedItem as string ?? "خودکار";
        DialogResult = true;
    }
}