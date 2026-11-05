using System.IO;
using System.Windows;
using Microsoft.Win32;
using BankStatementViewer.Services;

namespace BankStatementViewer.Views;

public partial class FtpSettingsWindow : Window
{
    private readonly FtpStatementService _service = new();
    public FtpConnectionSettings Settings { get; private set; }

    public FtpSettingsWindow(FtpConnectionSettings settings)
    {
        InitializeComponent();
        Settings = settings;
        AddressBox.Text = $"ftp://{settings.Host}:{(settings.Port == 21 ? "" : settings.Port.ToString())}{settings.RemotePath}";
        DownloadPathBox.Text = settings.DownloadDirectory;
        UserNameBox.Text = settings.UserName;
    }

    private FtpConnectionSettings ReadSettings()
    {
        if (!Uri.TryCreate(AddressBox.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeFtp)
            throw new ArgumentException("آدرس باید با ftp:// شروع شود.");
        return new FtpConnectionSettings
        {
            Host = uri.Host, Port = uri.IsDefaultPort ? 21 : uri.Port,
            RemotePath = string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : Uri.UnescapeDataString(uri.AbsolutePath),
            DownloadDirectory = DownloadPathBox.Text.Trim(),
            UserName = UserNameBox.Text.Trim(),
            ProtectedPassword = Settings.ProtectedPassword,
            Password = PasswordBox.Password
        };
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "انتخاب پوشه دانلود" };
        if (dialog.ShowDialog(this) == true) DownloadPathBox.Text = dialog.FolderName;
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = ReadSettings();
            if (!string.IsNullOrEmpty(settings.Password))
                settings.ProtectedPassword = Convert.ToBase64String(System.Security.Cryptography.ProtectedData.Protect(
                    System.Text.Encoding.UTF8.GetBytes(settings.Password), null, System.Security.Cryptography.DataProtectionScope.CurrentUser));
            await Task.Run(() => _service.TestConnection(settings));
            MessageBox.Show(this, "ارتباط با سرور FTP با موفقیت برقرار شد.", "تست ارتباط", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "خطا در اتصال FTP", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { Settings = ReadSettings(); DialogResult = true; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "تنظیمات FTP", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
