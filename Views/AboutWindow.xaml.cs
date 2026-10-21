using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace BankStatementViewer.Views;

public partial class AboutWindow : Window
{
    private const string RepositoryUrl = "https://github.com/Shahabs2004/BankStatementViewer";
    private const string ReleasesUrl = "https://github.com/Shahabs2004/BankStatementViewer/releases";

    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetEntryAssembly()?.GetName().Version
                      ?? Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"نسخه: {version?.ToString(3) ?? "نامشخص"}";
    }

    private void Repository_Click(object sender, RoutedEventArgs e) => OpenLink(RepositoryUrl);
    private void Releases_Click(object sender, RoutedEventArgs e) => OpenLink(ReleasesUrl);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void OpenLink(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"بازکردن پیوند امکان‌پذیر نبود:\n{ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
