using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentFTP;

namespace BankStatementViewer.Services;

public sealed class FtpConnectionSettings
{
    public string Host { get; set; } = "10.0.53.68";
    public string RemotePath { get; set; } = "/Statement";
    public int Port { get; set; } = 21;
    public string UserName { get; set; } = "";
    public string ProtectedPassword { get; set; } = "";
    [JsonIgnore] public string Password { get; set; } = "";
    public string DownloadDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
}

public sealed record FtpStatementFile(string Name, string RemotePath, long Size, DateTime Modified);

public sealed class FtpStatementService
{
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BankStatementViewer", "ftp.json");

    public FtpConnectionSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return new FtpConnectionSettings();
            return JsonSerializer.Deserialize<FtpConnectionSettings>(File.ReadAllText(_settingsPath)) ?? new FtpConnectionSettings();
        }
        catch { return new FtpConnectionSettings(); }
    }

    public void SaveSettings(FtpConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!string.IsNullOrWhiteSpace(settings.Password))
        {
            settings.ProtectedPassword = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(settings.Password), null, DataProtectionScope.CurrentUser));
            settings.Password = "";
        }
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var temp = _settingsPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _settingsPath, true);
    }

    public void TestConnection(FtpConnectionSettings settings)
    {
        using var client = CreateClient(settings);
        client.Connect();
    }

    public List<FtpStatementFile> ListExcelFiles(FtpConnectionSettings settings)
    {
        using var client = CreateClient(settings);
        client.Connect();
        var path = NormalizeRemotePath(settings.RemotePath);
        return client.GetListing(path)
            .Where(x => x.Type == FtpObjectType.File &&
                (x.Name.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) || x.Name.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)))
            .Select(x => new FtpStatementFile(x.Name, x.FullName, x.Size, x.Modified))
            .OrderByDescending(x => x.Modified)
            .ToList();
    }

    public string Download(FtpConnectionSettings settings, FtpStatementFile file)
    {
        Directory.CreateDirectory(settings.DownloadDirectory);
        var safeName = Path.GetFileName(file.Name);
        if (string.IsNullOrWhiteSpace(safeName)) throw new InvalidDataException("نام فایل FTP نامعتبر است.");
        var destination = Path.Combine(settings.DownloadDirectory, safeName);
        var temporary = destination + ".download";
        using var client = CreateClient(settings);
        client.Connect();
        var status = client.DownloadFile(temporary, file.RemotePath, FtpLocalExists.Overwrite, FtpVerify.Retry);
        if (status != FtpStatus.Success || !File.Exists(temporary))
            throw new IOException("دانلود فایل از FTP کامل نشد.");
        File.Move(temporary, destination, true);
        return destination;
    }

    private static FtpClient CreateClient(FtpConnectionSettings settings)
    {
        var password = "";
        if (!string.IsNullOrWhiteSpace(settings.ProtectedPassword))
            password = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(settings.ProtectedPassword), null, DataProtectionScope.CurrentUser));
        var client = new FtpClient(settings.Host, settings.UserName, password, settings.Port);
        client.Config.EncryptionMode = FtpEncryptionMode.None;
        client.Config.DataConnectionType = FtpDataConnectionType.AutoPassive;
        return client;
    }

    private static string NormalizeRemotePath(string path) => string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
}
