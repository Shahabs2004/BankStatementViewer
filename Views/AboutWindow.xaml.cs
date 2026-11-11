using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using BankStatementViewer.Services;

namespace BankStatementViewer.Views;

public partial class AboutWindow : Window
{
    private const string RepositoryUrl = "https://github.com/Shahabs2004/BankStatementViewer";
    private const string ReleasesUrl = "https://github.com/Shahabs2004/BankStatementViewer/releases";
    private const string ChangelogResourceName = "BankStatementViewer.CHANGELOG.md";

    public AboutWindow()
    {
        InitializeComponent();

        var currentVersion = GetBuiltVersion();
        VersionText.Text = $"نسخه: {currentVersion ?? "نامشخص"}";
        ChangelogList.ItemsSource = LoadChangelog(currentVersion);
    }

    /// <summary>Returns the version the running binary was built with (stamped by the release workflow), not a hardcoded value.</summary>
    private static string? GetBuiltVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = informational?.Split('+')[0].Trim();
        if (!string.IsNullOrWhiteSpace(version) && version != "1.0.0")
            return version;

        try
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
            var normalized = NormalizeFileVersion(fileVersion);
            if (normalized is not null)
                return normalized;
        }
        catch (ArgumentException)
        {
            // assembly.Location can be empty (single-file publish); fall back below.
        }

        return assembly.GetName().Version?.ToString(3);
    }

    private static string? NormalizeFileVersion(string? fileVersion)
    {
        if (string.IsNullOrWhiteSpace(fileVersion))
            return null;
        var parts = fileVersion.Split('.');
        var length = parts.Length;
        while (length > 2 && parts[length - 1] == "0")
            length--;
        return string.Join('.', parts, 0, length);
    }

    private static List<ChangelogEntry>? LoadChangelog(string? currentVersion)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(ChangelogResourceName);
            if (stream is null) return null;
            using var reader = new StreamReader(stream);
            return ChangelogParser.Parse(reader.ReadToEnd(), currentVersion);
        }
        catch (Exception)
        {
            return null;
        }
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
