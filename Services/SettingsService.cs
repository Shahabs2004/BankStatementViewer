using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BankStatementViewer.Models;

namespace BankStatementViewer.Services;

/// <summary>Stores user settings in a writable per-user location and migrates the old exe-folder file once.</summary>
public sealed class SettingsService
{
    private readonly string _settingsPath;
    private readonly string _legacySettingsPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public SettingsService(string? directory = null)
    {
        var settingsDirectory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BankStatementViewer");
        _settingsPath = Path.Combine(settingsDirectory, "appsettings.json");
        _legacySettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(_settingsPath))
            {
                // Upgrade path: preserve existing preferences from older versions that saved beside the exe.
                if (!string.Equals(Path.GetFullPath(_legacySettingsPath), Path.GetFullPath(_settingsPath), StringComparison.OrdinalIgnoreCase)
                    && File.Exists(_legacySettingsPath))
                {
                    var migrated = Deserialize(File.ReadAllText(_legacySettingsPath, Encoding.UTF8));
                    Save(migrated);
                    return migrated;
                }

                var defaults = CreateDefaults();
                Save(defaults);
                return defaults;
            }

            return Deserialize(File.ReadAllText(_settingsPath, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException)
        {
            warning = $"تنظیمات قبلی بارگذاری نشد و مقادیر پیش‌فرض استفاده می‌شود.\n{ex.Message}";
            var defaults = CreateDefaults();
            try
            {
                // Even when the old file is unreadable (for example under Program Files),
                // create a fresh writable settings file in the per-user AppData folder.
                Save(defaults);
            }
            catch (Exception saveException) when (saveException is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
            {
                warning += $"\nذخیره تنظیمات پیش‌فرض نیز ناموفق بود: {saveException.Message}";
            }
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var tempPath = _settingsPath + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.Move(tempPath, _settingsPath, overwrite: true);
    }

    private static AppSettings Deserialize(string json)
    {
        var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
            ?? throw new InvalidDataException("محتوای تنظیمات خالی یا نامعتبر است.");
        if (loaded.SearchPresets is null || loaded.SearchPresets.Count == 0) loaded.SearchPresets = CreateDefaults().SearchPresets;
        loaded.LastFilters ??= new FilterSettings();
        loaded.DisplayColumns ??= CreateDefaults().DisplayColumns;
        loaded.FontFamilyName = string.IsNullOrWhiteSpace(loaded.FontFamilyName) ? "Tahoma" : loaded.FontFamilyName;
        return loaded;
    }

    private static AppSettings CreateDefaults() => new()
    {
        SearchPresets = new List<SearchPreset>
        {
            new() { Name = "انتقال موبایلی", SearchText = "انتقالMOB" },
            new() { Name = "انتقال پایا", SearchText = "پایا" },
            new() { Name = "خرید با POS", SearchText = "خریدباPOS" },
            new() { Name = "واریز حقوق", SearchText = "واریز حقوق" },
            new() { Name = "برداشت پل", SearchText = "برداشت پل" },
            new() { Name = "ساتنا", SearchText = "ساتنا" },
            new() { Name = "چک", SearchText = "چک" }
        },
        LastFilters = new FilterSettings(),
        DisplayColumns = new AppSettings().DisplayColumns,
        FontFamilyName = "Tahoma",
        AutoFitColumns = true,
        HideFeeRows = false
    };
}
