using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using BankStatementViewer.Models;
using BankStatementViewer.Services;

namespace BankStatementViewer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly StatementParser _parser = new();
    private readonly SettingsService _settingsService = new();
    private readonly DispatcherTimer _filterDebounceTimer;
    private AppSettings _settings;
    private StatementHeader? _header;
    private string _currentFilePath = string.Empty;
    private string _summaryText = "ابتدا یک فایل صورت‌حساب انتخاب کنید.";
    private string _settingsStatus = string.Empty;
    private string _fontFamilyName = "Tahoma";
    private bool _autoFitColumns = true;
    private bool _hideFeeRows;
    private string _rowHeightOption = "خودکار";
    private List<string> _displayColumns = DefaultDisplayColumns();
    private bool _normalizingFilters;
    private int _filterUpdateDepth;
    private IReadOnlyList<StatementTransaction> _matchedTransactions = Array.Empty<StatementTransaction>();

    public static IReadOnlyList<string> AvailableFontFamilies { get; } = new[]
    {
        "Tahoma", "Segoe UI", "Arial", "Calibri", "Verdana", "Microsoft Sans Serif",
        "B Nazanin", "B Mitra", "B Yekan", "Vazirmatn"
    };
    public IReadOnlyList<string> AvailableFonts => AvailableFontFamilies;
    public IReadOnlyList<string> AvailableRowHeightOptions { get; } = new[] { "خودکار", "کم", "متوسط", "زیاد" };

    public MainViewModel()
    {
        _settings = _settingsService.Load(out var warning);
        Filters = _settings.LastFilters ?? new FilterSettings();
        Presets = new ObservableCollection<SearchPreset>(_settings.SearchPresets ?? new List<SearchPreset>());

        // Old versions did not include parsed-description columns. Upgrade old defaults while preserving new-format customizations.
        var savedColumns = _settings.DisplayColumns ?? new List<string>();
        var migratedColumns = savedColumns.Count == 0 || !savedColumns.Contains("Channel", StringComparer.Ordinal);
        _displayColumns = migratedColumns ? DefaultDisplayColumns() : NormalizeDisplayColumns(savedColumns);
        _settings.DisplayColumns = _displayColumns.ToList();

        _fontFamilyName = AvailableFontFamilies.Contains(_settings.FontFamilyName) ? _settings.FontFamilyName : "Tahoma";
        _settings.FontFamilyName = _fontFamilyName;
        _autoFitColumns = _settings.AutoFitColumns;
        _hideFeeRows = _settings.HideFeeRows;
        _rowHeightOption = AvailableRowHeightOptions.Contains(_settings.RowHeightOption)
            ? _settings.RowHeightOption
            : "خودکار";
        _settings.RowHeightOption = _rowHeightOption;

        _filterDebounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _filterDebounceTimer.Tick += (_, _) =>
        {
            _filterDebounceTimer.Stop();
            ApplyFiltersNow();
        };

        Filters.PropertyChanged += Filters_PropertyChanged;
        if (!string.IsNullOrWhiteSpace(warning)) SettingsStatus = warning;
        NormalizeMutuallyExclusiveAmountFilters();
        if (migratedColumns) SaveSettingsQuietly();
        ApplyFiltersNow();
    }

    public FilterSettings Filters { get; }
    public ObservableCollection<SearchPreset> Presets { get; }
    public BulkObservableCollection<StatementTransaction> AllTransactions { get; } = new();
    public BulkObservableCollection<StatementTransaction> FilteredTransactions { get; } = new();
    public StatementHeader? Header { get => _header; private set { if (SetField(ref _header, value)) OnPropertyChanged(nameof(HasStatement)); } }
    public bool HasStatement => Header is not null && AllTransactions.Count > 0;
    public string CurrentFilePath { get => _currentFilePath; private set => SetField(ref _currentFilePath, value); }
    public string SummaryText { get => _summaryText; private set => SetField(ref _summaryText, value); }
    public string SettingsStatus { get => _settingsStatus; private set => SetField(ref _settingsStatus, value); }
    public IReadOnlyList<string> DisplayColumns => _displayColumns.AsReadOnly();
    public IReadOnlyList<StatementTransaction> MatchedTransactions
    {
        get => _matchedTransactions;
        private set => SetField(ref _matchedTransactions, value);
    }

    public string FontFamilyName
    {
        get => _fontFamilyName;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !AvailableFontFamilies.Contains(value)) return;
            if (SetField(ref _fontFamilyName, value))
            {
                _settings.FontFamilyName = value;
                SaveSettingsQuietly();
            }
        }
    }

    public bool AutoFitColumns
    {
        get => _autoFitColumns;
        set
        {
            if (SetField(ref _autoFitColumns, value))
            {
                _settings.AutoFitColumns = value;
                SaveSettingsQuietly();
            }
        }
    }

    public bool HideFeeRows
    {
        get => _hideFeeRows;
        set
        {
            if (SetField(ref _hideFeeRows, value))
            {
                _settings.HideFeeRows = value;
                SaveSettingsQuietly();
                ScheduleApplyFilters();
            }
        }
    }

    public string RowHeightOption
    {
        get => _rowHeightOption;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || !AvailableRowHeightOptions.Contains(value)) return;
            if (SetField(ref _rowHeightOption, value))
            {
                _settings.RowHeightOption = value;
                SaveSettingsQuietly();
            }
        }
    }

    public bool CanUseDepositAmountFilter => !(Filters.TypeFilterEnabled && Filters.TypeFilterMode == "برداشت");
    public bool CanUseWithdrawalAmountFilter => !(Filters.TypeFilterEnabled && Filters.TypeFilterMode == "واریز");
    public string SettingsPath => _settingsService.SettingsPath;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void LoadStatement(string path)
    {
        var data = _parser.Parse(path);
        Header = data.Header;
        CurrentFilePath = path;
        AllTransactions.ReplaceAll(data.Transactions);
        OnPropertyChanged(nameof(HasStatement));
        ApplyFilters();
        SettingsStatus = $"فایل بارگذاری شد: {Path.GetFileName(path)}";
    }

    /// <summary>Immediate refresh for explicit actions such as loading a new statement.</summary>
    public void ApplyFilters()
    {
        _filterDebounceTimer.Stop();
        ApplyFiltersNow();
    }

    public void ClearFilters()
    {
        _filterUpdateDepth++;
        try
        {
            var f = Filters;
            f.TypeFilterEnabled = false;
            f.TypeFilterMode = "واریز";
            f.BranchFilterEnabled = false;
            f.BranchCode = string.Empty;
            f.TrackingFilterEnabled = false;
            f.TrackingText = string.Empty;
            f.DateFilterEnabled = false;
            f.DateFrom = string.Empty;
            f.DateTo = string.Empty;
            f.RowNumberFilterEnabled = false;
            f.RowFrom = 1;
            f.RowTo = 999999;
            f.DepositAmountFilterEnabled = false;
            f.DepositAmountMode = "Range";
            f.DepositAmountFrom = 0;
            f.DepositAmountTo = 0;
            f.WithdrawalAmountFilterEnabled = false;
            f.WithdrawalAmountMode = "Range";
            f.WithdrawalAmountFrom = 0;
            f.WithdrawalAmountTo = 0;
            f.DescriptionFilterEnabled = false;
            f.DescriptionSearchText = string.Empty;
            f.TextSearchEnabled = false;
            f.TextSearchText = string.Empty;
        }
        finally
        {
            _filterUpdateDepth--;
        }

        OnPropertyChanged(nameof(CanUseDepositAmountFilter));
        OnPropertyChanged(nameof(CanUseWithdrawalAmountFilter));
        ScheduleApplyFilters();
    }

    public void AddPreset(string name, string searchText)
    {
        name = name.Trim();
        searchText = searchText.Trim();
        if (name.Length == 0 || searchText.Length == 0) throw new ArgumentException("نام و متن جستجو نباید خالی باشند.");
        if (Presets.Any(p => TextNormalizer.EqualsNormalized(p.Name, name)))
            throw new InvalidOperationException("پیش‌فرضی با این نام وجود دارد.");
        Presets.Add(new SearchPreset { Name = name, SearchText = searchText });
        SaveSettings();
    }

    public void RemovePreset(SearchPreset? preset)
    {
        if (preset is null) return;
        Presets.Remove(preset);
        SaveSettings();
    }

    public void SetDisplayColumns(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var requested = new HashSet<string>(keys, StringComparer.Ordinal);
        var normalized = TransactionColumnCatalog.All.Where(c => requested.Contains(c.Key)).Select(c => c.Key).ToList();
        if (normalized.Count == 0) throw new InvalidOperationException("حداقل یک ستون برای نمایش انتخاب کنید.");

        _displayColumns = normalized;
        _settings.DisplayColumns = normalized.ToList();
        OnPropertyChanged(nameof(DisplayColumns));
        SaveSettingsQuietly();
    }

    public void SaveSettings()
    {
        try
        {
            _settings.LastFilters = Filters;
            _settings.SearchPresets = Presets.ToList();
            _settings.DisplayColumns = _displayColumns.ToList();
            _settings.FontFamilyName = _fontFamilyName;
            _settings.AutoFitColumns = _autoFitColumns;
            _settings.HideFeeRows = _hideFeeRows;
            _settings.RowHeightOption = _rowHeightOption;
            _settingsService.Save(_settings);
            SettingsStatus = $"تنظیمات ذخیره شد: {_settingsService.SettingsPath}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        {
            SettingsStatus = $"ذخیره تنظیمات ناموفق بود: {ex.Message}";
            throw;
        }
    }

    private void Filters_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_filterUpdateDepth > 0 || _normalizingFilters) return;

        if (e.PropertyName is nameof(FilterSettings.TypeFilterEnabled) or nameof(FilterSettings.TypeFilterMode))
        {
            OnPropertyChanged(nameof(CanUseDepositAmountFilter));
            OnPropertyChanged(nameof(CanUseWithdrawalAmountFilter));
            NormalizeMutuallyExclusiveAmountFilters();
        }
        ScheduleApplyFilters();
    }

    private void NormalizeMutuallyExclusiveAmountFilters()
    {
        if (_normalizingFilters || !Filters.TypeFilterEnabled) return;
        _normalizingFilters = true;
        try
        {
            if (Filters.TypeFilterMode == "واریز" && Filters.WithdrawalAmountFilterEnabled)
                Filters.WithdrawalAmountFilterEnabled = false;
            else if (Filters.TypeFilterMode == "برداشت" && Filters.DepositAmountFilterEnabled)
                Filters.DepositAmountFilterEnabled = false;
        }
        finally { _normalizingFilters = false; }
    }

    private void ScheduleApplyFilters()
    {
        if (_filterUpdateDepth > 0) return;
        _filterDebounceTimer.Stop();
        _filterDebounceTimer.Start();
    }

    private void ApplyFiltersNow()
    {
        IEnumerable<StatementTransaction> query = AllTransactions;
        var f = Filters;

        if (f.TypeFilterEnabled)
        {
            query = f.TypeFilterMode == "برداشت"
                ? query.Where(t => t.Withdrawal > 0 && t.Deposit == 0)
                : query.Where(t => t.Deposit > 0 && t.Withdrawal == 0);
        }

        if (f.BranchFilterEnabled && !string.IsNullOrWhiteSpace(f.BranchCode))
        {
            var branch = TextNormalizer.Normalize(f.BranchCode);
            query = query.Where(t => TextNormalizer.Normalize(t.BranchCode) == branch);
        }

        if (f.TrackingFilterEnabled && !string.IsNullOrWhiteSpace(f.TrackingText))
            query = query.Where(t => TextNormalizer.Contains(t.Tracking, f.TrackingText) || TextNormalizer.Contains(t.TrackingDeposit, f.TrackingText));

        if (f.DateFilterEnabled)
        {
            var from = NormalizeFilterDate(f.DateFrom);
            var to = NormalizeFilterDate(f.DateTo);
            if (!string.IsNullOrWhiteSpace(from)) query = query.Where(t => string.CompareOrdinal(StatementParser.NormalizeDate(t.Date), from) >= 0);
            if (!string.IsNullOrWhiteSpace(to)) query = query.Where(t => string.CompareOrdinal(StatementParser.NormalizeDate(t.Date), to) <= 0);
        }

        if (f.RowNumberFilterEnabled)
        {
            var from = Math.Min(f.RowFrom, f.RowTo);
            var to = Math.Max(f.RowFrom, f.RowTo);
            query = query.Where(t => t.RowNumber >= from && t.RowNumber <= to);
        }

        if (f.DepositAmountFilterEnabled && CanUseDepositAmountFilter)
            query = query.Where(t => t.Deposit > 0 && MatchAmount(t.Deposit, f.DepositAmountMode, f.DepositAmountFrom, f.DepositAmountTo));

        if (f.WithdrawalAmountFilterEnabled && CanUseWithdrawalAmountFilter)
            query = query.Where(t => t.Withdrawal > 0 && MatchAmount(t.Withdrawal, f.WithdrawalAmountMode, f.WithdrawalAmountFrom, f.WithdrawalAmountTo));

        if (f.DescriptionFilterEnabled && !string.IsNullOrWhiteSpace(f.DescriptionSearchText))
            query = query.Where(t => ContainsTransactionText(t, f.DescriptionSearchText));

        if (f.TextSearchEnabled && !string.IsNullOrWhiteSpace(f.TextSearchText))
            query = query.Where(t => ContainsTransactionText(t, f.TextSearchText));

        var matched = query.ToList();
        MatchedTransactions = matched;
        var feeTotal = matched.Where(t => t.IsFee).Sum(t => t.FeeAmount);
        var visible = _hideFeeRows ? matched.Where(t => !t.IsFee).ToList() : matched;
        FilteredTransactions.ReplaceAll(visible);

        // Keep operational totals separate from fee rows so the fee amount is not silently
        // mixed into the transfer totals when fee rows are shown or hidden.
        var regularMatched = matched.Where(t => !t.IsFee).ToList();
        var depositTotal = regularMatched.Sum(t => t.Deposit);
        var withdrawalTotal = regularMatched.Sum(t => t.Withdrawal);
        SummaryText = $"تعداد نمایش‌داده‌شده: {visible.Count:N0} از {AllTransactions.Count:N0}  |  جمع واریز تراکنش‌ها: {depositTotal:N0}  |  جمع برداشت تراکنش‌ها: {withdrawalTotal:N0}  |  جمع کارمزد: {feeTotal:N0}";
        OnPropertyChanged(nameof(HasStatement));
    }

    private static bool ContainsTransactionText(StatementTransaction transaction, string search) =>
        TextNormalizer.Contains(transaction.Description, search) ||
        TextNormalizer.Contains(transaction.RawDescription, search) ||
        TextNormalizer.Contains(transaction.Channel, search) ||
        TextNormalizer.Contains(transaction.OtherAccount, search) ||
        TextNormalizer.Contains(transaction.Purpose, search);

    private void SaveSettingsQuietly()
    {
        try { SaveSettings(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        {
            // SettingsStatus explains the failure; the main workflow remains usable.
        }
    }

    private static List<string> DefaultDisplayColumns() => new()
    {
        "RowNumber", "Date", "Time", "TransactionType", "EntryKind", "Channel", "Deposit", "Withdrawal",
        "Balance", "BranchCode", "Tracking", "OtherAccount", "Purpose", "Description"
    };

    private static List<string> NormalizeDisplayColumns(IEnumerable<string>? keys)
    {
        var requested = new HashSet<string>(keys ?? Array.Empty<string>(), StringComparer.Ordinal);
        var result = TransactionColumnCatalog.All.Where(c => requested.Contains(c.Key)).Select(c => c.Key).ToList();
        return result.Count > 0 ? result : DefaultDisplayColumns();
    }

    private static bool MatchAmount(decimal value, string mode, decimal from, decimal to) => mode switch
    {
        "LessThan" => value < from,
        "GreaterThan" => value > from,
        _ => value >= Math.Min(from, to) && value <= Math.Max(from, to)
    };

    private static bool Contains(string? source, string value) => TextNormalizer.Contains(source, value);

    private static string NormalizeFilterDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = StatementParser.NormalizeDate(value.Trim());
        return System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^\d{4}/\d{2}/\d{2}$") ? normalized : string.Empty;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
