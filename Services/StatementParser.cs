using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;
using BankStatementViewer.Models;

namespace BankStatementViewer.Services;

/// <summary>
/// Supports both real Excel workbooks (.xls/.xlsx) and bank-export text files whose extension may be .XLS.
/// The parser intentionally discovers columns by their headings instead of relying only on fixed positions.
/// </summary>
public sealed class StatementParser
{
    private const string OleMagic = "D0CF11E0A1B11AE1";
    private const string ZipMagic = "504B0304";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    static StatementParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public StatementData Parse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("مسیر فایل مشخص نشده است.", nameof(filePath));
        if (!File.Exists(filePath)) throw new FileNotFoundException("فایل انتخاب‌شده پیدا نشد.", filePath);

        var bytes = File.ReadAllBytes(filePath);
        if (bytes.Length == 0) throw new InvalidDataException("فایل انتخاب‌شده خالی است.");

        var signature = Convert.ToHexString(bytes.AsSpan(0, Math.Min(8, bytes.Length)));
        if (signature.StartsWith(OleMagic, StringComparison.Ordinal) || signature.StartsWith(ZipMagic, StringComparison.Ordinal))
            return ParseExcel(filePath);

        // If the extension promises an Excel workbook but neither Excel signature is present,
        // text parsing is still attempted: many bank portals export TSV with an .XLS suffix.
        return ParseText(filePath, bytes);
    }

    private static StatementData ParseExcel(string filePath)
    {
        var sheets = new List<SheetRows>();
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream, new ExcelReaderConfiguration
            {
                FallbackEncoding = Encoding.GetEncoding(1252),
                LeaveOpen = false
            });

            do
            {
                var rows = new List<List<string>>();
                while (reader.Read())
                {
                    var row = new List<string>(reader.FieldCount);
                    for (var col = 0; col < reader.FieldCount; col++) row.Add(CellToString(reader.GetValue(col)));
                    rows.Add(row);
                }
                sheets.Add(new SheetRows(reader.Name, rows));
            } while (reader.NextResult());
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException(
                "فایل دارای امضای فایل Excel است، اما خواندن ساختار آن ناموفق بود. فایل را در Excel باز و دوباره ذخیره کنید یا نسخه متنی TSV بگیرید.\n" + ex.Message, ex);
        }

        return ParseSheets(filePath, sheets);
    }

    private static StatementData ParseText(string filePath, byte[] bytes)
    {
        string text;
        try
        {
            text = DecodeText(bytes);
        }
        catch (Exception ex) when (ex is DecoderFallbackException or ArgumentException)
        {
            throw new InvalidDataException("کدگذاری فایل قابل تشخیص نیست. فایل را با UTF-8، Windows-1256 یا Windows-1252 ذخیره کنید.", ex);
        }

        text = text.TrimStart('\uFEFF', '\u0000');
        var rows = new List<List<string>>();
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.IndexOf('\0') >= 0) line = line.Replace("\0", string.Empty, StringComparison.Ordinal);
            rows.Add(SplitLine(line).Select(CleanCell).ToList());
        }
        return ParseSheets(filePath, new[] { new SheetRows(Path.GetFileName(filePath), rows) });
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(bytes);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return new UTF8Encoding(true, true).GetString(bytes);

        // Some exports omit a BOM despite being UTF-16.
        var sampleLength = Math.Min(bytes.Length, 256);
        var evenNuls = 0;
        var oddNuls = 0;
        for (var i = 0; i < sampleLength; i++)
        {
            if (bytes[i] != 0) continue;
            if (i % 2 == 0) evenNuls++; else oddNuls++;
        }
        if (oddNuls > sampleLength / 8 && evenNuls == 0) return Encoding.Unicode.GetString(bytes);
        if (evenNuls > sampleLength / 8 && oddNuls == 0) return Encoding.BigEndianUnicode.GetString(bytes);

        try { return StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException) { }

        try
        {
            var windows1256 = Encoding.GetEncoding(1256, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            return windows1256.GetString(bytes);
        }
        catch (DecoderFallbackException) { }

        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(bytes);
    }

    private static List<string> SplitLine(string line)
    {
        if (line.Contains('\t')) return line.Split('\t').ToList();
        if (line.Contains('|'))
        {
            var cells = line.Split('|').ToList();
            if (cells.Count > 0 && string.IsNullOrWhiteSpace(cells[0])) cells.RemoveAt(0);
            if (cells.Count > 0 && string.IsNullOrWhiteSpace(cells[^1])) cells.RemoveAt(cells.Count - 1);
            return cells;
        }
        if (line.Count(c => c == ';') >= 10) return line.Split(';').ToList();
        if (line.Count(c => c == ',') >= 10) return line.Split(',').ToList();
        return new List<string> { line };
    }

    private static string CleanCell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var cleaned = value.Replace("\uFEFF", string.Empty, StringComparison.Ordinal)
            .Replace('\u00A0', ' ')
            .Replace('\u200F', ' ')
            .Replace('\u200E', ' ')
            .Replace('\u202A', ' ')
            .Replace('\u202B', ' ')
            .Replace('\u202C', ' ')
            .Trim();
        if (cleaned.Length >= 2 && cleaned[0] == '"' && cleaned[^1] == '"')
            cleaned = cleaned[1..^1].Trim();
        return cleaned;
    }

    private static string CellToString(object? value)
    {
        if (value is null or DBNull) return string.Empty;
        if (value is DateTime dt) return dt.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        if (value is double d) return d.ToString("0.################", CultureInfo.InvariantCulture);
        if (value is float f) return f.ToString("0.################", CultureInfo.InvariantCulture);
        if (value is decimal dec) return dec.ToString(CultureInfo.InvariantCulture);
        return CleanCell(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private static StatementData ParseSheets(string filePath, IEnumerable<SheetRows> sheets)
    {
        var candidates = sheets
            .Select(sheet => new { Sheet = sheet, Match = FindHeader(sheet.Rows) })
            .Where(x => x.Match is not null)
            .OrderByDescending(x => x.Match!.Score)
            .ToList();

        if (candidates.Count == 0)
        {
            var first = sheets.FirstOrDefault();
            var sample = BuildSample(first?.Rows ?? new List<List<string>>());
            throw new InvalidDataException(
                "ردیف سرستون‌های گردش حساب پیدا نشد. برای تشخیص جدول، حداقل ستون‌های توضیحات، مانده و واریز/برداشت لازم است.\n" +
                "نمونه محتوای خوانده‌شده از ابتدای فایل:\n" + sample +
                "\nاگر فایل Excel واقعی است، بررسی کنید شیت درست و داده‌های تراکنش داخل آن قرار داشته باشند.");
        }

        var selected = candidates[0];
        var rows = selected.Sheet.Rows;
        var headerIndex = selected.Match!.RowIndex;
        var map = selected.Match.Columns;
        var header = ExtractStatementHeader(rows, headerIndex, filePath);
        var transactions = new List<StatementTransaction>();
        decimal? bankOpeningBalance = null;
        var nextDisplayRow = 1;

        for (var i = headerIndex + 1; i < rows.Count; i++)
        {
            var cells = rows[i].Select(CleanCell).ToList();
            var lineText = string.Join(" ", cells.Where(c => !string.IsNullOrWhiteSpace(c)));
            if (lineText.Contains("مهر و امضاء مجاز", StringComparison.OrdinalIgnoreCase) ||
                lineText.Contains("این صورتحساب بدون مهر", StringComparison.OrdinalIgnoreCase) ||
                lineText.Contains("این صورت حساب بدون مهر", StringComparison.OrdinalIgnoreCase)) break;

            if (IsHeaderRow(cells)) continue;

            var description = GetCell(cells, map.Description);
            if (TextNormalizer.EqualsNormalized(description, "مانده از صفحه قبل"))
            {
                var rawBalance = GetCell(cells, map.Balance);
                if (!string.IsNullOrWhiteSpace(rawBalance))
                    bankOpeningBalance ??= ParseAmount(rawBalance);
                else
                    bankOpeningBalance ??= TryGetRepeatedOpeningBalance(cells, map);
                continue;
            }

            if (StartsWithBreakTag(description))
            {
                var continuation = Regex.Replace(description, @"^\s*<br\s*/?>", string.Empty, RegexOptions.IgnoreCase).Trim();
                if (transactions.Count > 0 && continuation.Length > 0)
                {
                    var prior = transactions[^1];
                    var combined = string.IsNullOrWhiteSpace(prior.RawDescription)
                        ? continuation
                        : prior.RawDescription + Environment.NewLine + continuation;
                    TransactionDescriptionParser.Populate(prior, combined);
                }
                continue;
            }

            var date = NormalizeDate(GetCell(cells, map.Date));
            var deposit = ParseAmount(GetCell(cells, map.Deposit));
            var withdrawal = ParseAmount(GetCell(cells, map.Withdrawal));
            var balance = ParseAmount(GetCell(cells, map.Balance));
            var rowNumberText = GetCell(cells, map.RowNumber);
            var rowNumberOk = TryParseInt(rowNumberText, out var originalRowNumber) && originalRowNumber > 0;
            var hasAnyTransactionContent = !string.IsNullOrWhiteSpace(description) || deposit != 0 || withdrawal != 0;

            // Prefer the statement row number when present. For alternate bank layouts, a valid
            // date plus transaction content is enough, and an ordinal is assigned if no row exists.
            if (!rowNumberOk && (string.IsNullOrWhiteSpace(date) || !hasAnyTransactionContent)) continue;
            if (rowNumberOk && !hasAnyTransactionContent && string.IsNullOrWhiteSpace(date)) continue;

            var transaction = new StatementTransaction
            {
                RowNumber = rowNumberOk ? originalRowNumber : nextDisplayRow,
                Date = date,
                Time = NormalizeTime(GetCell(cells, map.Time)),
                Iban = NormalizeIban(GetCell(cells, map.Iban)),
                Deposit = deposit,
                Withdrawal = withdrawal,
                Balance = balance,
                BranchCode = GetCell(cells, map.BranchCode),
                Tracking = GetCell(cells, map.Tracking),
                Identifier = GetCell(cells, map.Identifier),
                TrackingDeposit = GetCell(cells, map.TrackingDeposit),
                Counterparty = GetCell(cells, map.Counterparty)
            };
            TransactionDescriptionParser.Populate(transaction, description);
            transactions.Add(transaction);
            nextDisplayRow++;
        }

        if (transactions.Count == 0)
            throw new InvalidDataException("سرستون‌ها پیدا شدند، اما هیچ ردیف تراکنش معتبری استخراج نشد. ساختار ستون‌ها یا نمونه فایل با الگوی پشتیبانی‌شده متفاوت است.");

        var calculatedOpeningBalance = transactions[0].Balance - transactions[0].Deposit + transactions[0].Withdrawal;
        header.OpeningBalance = bankOpeningBalance ?? calculatedOpeningBalance;
        DetectFeeRows(transactions);
        header.ClosingBalance = transactions[^1].Balance;
        header.SourceFileName = Path.GetFileName(filePath);

        if (string.IsNullOrWhiteSpace(header.Iban))
            header.Iban = transactions.Select(t => t.Iban).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(header.BranchCode))
            header.BranchCode = transactions.Select(t => t.BranchCode).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty;

        return new StatementData { Header = header, Transactions = transactions };
    }

    private static decimal? TryGetRepeatedOpeningBalance(IReadOnlyList<string> cells, ColumnMap map)
    {
        var excluded = new HashSet<int> { map.Description, map.Tracking, map.BranchCode, map.Time, map.Date, map.RowNumber, map.Identifier };
        var numericValues = new List<decimal>();
        for (var i = 0; i < cells.Count; i++)
        {
            if (excluded.Contains(i) || string.IsNullOrWhiteSpace(cells[i])) continue;
            var parsed = ParseAmount(cells[i]);
            if (parsed != 0m) numericValues.Add(parsed);
        }

        var repeated = numericValues.GroupBy(v => v).OrderByDescending(g => g.Count()).FirstOrDefault(g => g.Count() >= 2);
        return repeated?.Key;
    }

    private static void DetectFeeRows(IReadOnlyList<StatementTransaction> transactions)
    {
        for (var i = 1; i < transactions.Count; i++)
        {
            var current = transactions[i];
            var prior = transactions[i - 1];
            if (current.IsFee || prior.IsFee) continue;
            var currentTracking = FirstNonEmpty(current.Tracking, current.TrackingDeposit);
            var priorTracking = FirstNonEmpty(prior.Tracking, prior.TrackingDeposit);
            var sameTracking = !string.IsNullOrWhiteSpace(current.Tracking) &&
                               !string.IsNullOrWhiteSpace(prior.Tracking) &&
                               TextNormalizer.EqualsNormalized(current.Tracking, prior.Tracking);
            sameTracking |= !string.IsNullOrWhiteSpace(current.TrackingDeposit) &&
                            !string.IsNullOrWhiteSpace(prior.TrackingDeposit) &&
                            TextNormalizer.EqualsNormalized(current.TrackingDeposit, prior.TrackingDeposit);
            sameTracking |= !string.IsNullOrWhiteSpace(current.Tracking) &&
                            !string.IsNullOrWhiteSpace(prior.TrackingDeposit) &&
                            TextNormalizer.EqualsNormalized(current.Tracking, prior.TrackingDeposit);
            sameTracking |= !string.IsNullOrWhiteSpace(current.TrackingDeposit) &&
                            !string.IsNullOrWhiteSpace(prior.Tracking) &&
                            TextNormalizer.EqualsNormalized(current.TrackingDeposit, prior.Tracking);
            sameTracking |= !string.IsNullOrWhiteSpace(currentTracking) &&
                            !string.IsNullOrWhiteSpace(priorTracking) &&
                            TextNormalizer.EqualsNormalized(currentTracking, priorTracking);

            var knownChannelsConflict = !TextNormalizer.EqualsNormalized(current.Channel, "سایر") &&
                                       !TextNormalizer.EqualsNormalized(prior.Channel, "سایر") &&
                                       !TextNormalizer.EqualsNormalized(current.Channel, prior.Channel);
            if (string.IsNullOrWhiteSpace(currentTracking) || !sameTracking ||
                !TextNormalizer.EqualsNormalized(current.Date, prior.Date) ||
                !TextNormalizer.EqualsNormalized(current.Time, prior.Time) ||
                knownChannelsConflict) continue;

            var currentAmount = Math.Abs(current.Deposit) + Math.Abs(current.Withdrawal);
            var priorAmount = Math.Abs(prior.Deposit) + Math.Abs(prior.Withdrawal);
            var sameDirection = (current.Deposit > 0 && prior.Deposit > 0) || (current.Withdrawal > 0 && prior.Withdrawal > 0);
            if (sameDirection && currentAmount > 0 && priorAmount > currentAmount)
                current.IsFee = true;
        }
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static HeaderMatch? FindHeader(List<List<string>> rows)
    {
        HeaderMatch? best = null;
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var cells = rows[rowIndex].Select(CleanCell).ToList();
            var normalized = cells.Select(NormalizeText).ToList();
            var hasDescription = normalized.Any(IsDescriptionHeader);
            var hasBalance = normalized.Any(s => s.Contains("مانده", StringComparison.Ordinal) || s.Equals("balance", StringComparison.OrdinalIgnoreCase));
            var hasDeposit = normalized.Any(IsDepositHeader);
            var hasWithdrawal = normalized.Any(IsWithdrawalHeader);
            var hasIban = normalized.Any(s => s.Contains("شبا", StringComparison.Ordinal) || s.Contains("iban", StringComparison.OrdinalIgnoreCase));
            var primary = hasDescription && hasBalance && (hasDeposit || hasWithdrawal);
            var secondary = hasDescription && hasBalance && hasDeposit && hasWithdrawal;
            if (!primary && !secondary) continue;

            var score = 5 + (hasIban ? 2 : 0) + (hasDeposit && hasWithdrawal ? 2 : 0) + (normalized.Any(s => s.Contains("تاریخ") || s.Equals("date", StringComparison.OrdinalIgnoreCase)) ? 1 : 0);
            var map = BuildColumnMap(cells, hasIban && cells.Count >= 13);
            if (best is null || score > best.Score) best = new HeaderMatch(rowIndex, map, score);
        }
        return best;
    }

    private static ColumnMap BuildColumnMap(List<string> headers, bool legacyFallback)
    {
        var normalized = headers.Select(NormalizeText).ToList();
        int Find(Predicate<string> predicate) => normalized.FindIndex(predicate);
        var map = new ColumnMap
        {
            Iban = Find(s => s.Contains("شبا") || s.Contains("iban")),
            Description = Find(IsDescriptionHeader),
            TrackingDeposit = Find(s => s.Contains("پیگیری") && s.Contains("واریز")),
            Identifier = Find(s => s.Contains("شناسه") || s.Equals("identifier", StringComparison.OrdinalIgnoreCase) || s.Equals("id", StringComparison.OrdinalIgnoreCase)),
            Counterparty = Find(s => (s.Contains("واریز کننده") || s.Contains("واریزکننده") || s.Contains("دریافت کننده") || s.Contains("دریافتکننده") || s.Contains("پرداخت کننده") || s.Contains("پرداختکننده") || s.Contains("counterparty")) && !s.Contains("پیگیری")),
            Balance = Find(s => s.Contains("مانده") || s.Equals("balance", StringComparison.OrdinalIgnoreCase)),
            Deposit = Find(IsDepositHeader),
            Withdrawal = Find(IsWithdrawalHeader),
            BranchCode = Find(s => s.Contains("شعبه") || s.Equals("branch", StringComparison.OrdinalIgnoreCase) || s.Contains("branch code")),
            Tracking = Find(s => s.Contains("پیگیری") && !s.Contains("واریز") || s.Contains("tracking")),
            Time = Find(s => s == "زمان" || s.Contains("ساعت") || s.Equals("time", StringComparison.OrdinalIgnoreCase)),
            Date = Find(s => (s == "تاریخ" || s.Contains("تاریخ تراکنش") || s.Equals("date", StringComparison.OrdinalIgnoreCase) || s.Contains("transaction date")) && !s.Contains("از تاریخ") && !s.Contains("تا تاریخ") && !s.Contains("دریافت")),
            RowNumber = Find(s => s == "ردیف" || s.Contains("شماره ردیف") || s.Equals("row", StringComparison.OrdinalIgnoreCase) || s.Equals("row number", StringComparison.OrdinalIgnoreCase))
        };

        if (legacyFallback)
        {
            map.Iban = Fallback(map.Iban, 0);
            map.Description = Fallback(map.Description, 1);
            map.TrackingDeposit = Fallback(map.TrackingDeposit, 2);
            map.Identifier = Fallback(map.Identifier, 3);
            map.Counterparty = Fallback(map.Counterparty, 4);
            map.Balance = Fallback(map.Balance, 5);
            map.Deposit = Fallback(map.Deposit, 6);
            map.Withdrawal = Fallback(map.Withdrawal, 7);
            map.BranchCode = Fallback(map.BranchCode, 8);
            map.Tracking = Fallback(map.Tracking, 9);
            map.Time = Fallback(map.Time, 10);
            map.Date = Fallback(map.Date, 11);
            map.RowNumber = Fallback(map.RowNumber, 12);
        }
        return map;
    }

    private static int Fallback(int found, int fallback) => found >= 0 ? found : fallback;
    private static bool IsDescriptionHeader(string s) => s.Contains("توضیحات") || s == "شرح" || s.Contains("شرح تراکنش") || s.Equals("description", StringComparison.OrdinalIgnoreCase) || s.Equals("narration", StringComparison.OrdinalIgnoreCase);
    private static bool IsDepositHeader(string s) =>
        (s == "واریز" || s.StartsWith("واریز ", StringComparison.Ordinal) || s.Contains("مبلغ واریز") ||
         s.Equals("deposit", StringComparison.OrdinalIgnoreCase) || s.Equals("credit", StringComparison.OrdinalIgnoreCase) || s.Contains("credit amount")) &&
        !s.Contains("پیگیری") && !s.Contains("واریز کننده") && !s.Contains("واریزکننده");
    private static bool IsWithdrawalHeader(string s) =>
        (s == "برداشت" || s.StartsWith("برداشت ", StringComparison.Ordinal) || s.Contains("مبلغ برداشت") ||
         s.Equals("withdrawal", StringComparison.OrdinalIgnoreCase) || s.Equals("debit", StringComparison.OrdinalIgnoreCase) || s.Contains("debit amount")) &&
        !s.Contains("پیگیری");

    private static bool IsHeaderRow(List<string> cells)
    {
        var values = cells.Select(NormalizeText).ToList();
        return values.Any(IsDescriptionHeader) && values.Any(s => s.Contains("مانده")) && values.Any(IsDepositHeader);
    }

    private static StatementHeader ExtractStatementHeader(List<List<string>> rows, int tableHeaderIndex, string filePath)
    {
        var result = new StatementHeader { SourceFileName = Path.GetFileName(filePath) };
        var scanLimit = Math.Min(rows.Count, Math.Max(tableHeaderIndex, 12));

        result.ReportTo = FindLabeledValue(rows, scanLimit, s => s.Contains("تا تاریخ"), preferNext: false);
        result.ReportFrom = FindLabeledValue(rows, scanLimit, s => s.Contains("از تاریخ"), preferNext: false);
        result.BranchCode = FindLabeledValue(rows, scanLimit, s => s.Contains("کد شعبه"), preferNext: false);
        result.Iban = NormalizeIban(FindLabeledValue(rows, scanLimit, s => s.Contains("شماره حساب شبا") || s == "شبا" || s.Contains("شماره شبا"), preferNext: true));
        result.ReceivedDate = FindLabeledValue(rows, scanLimit, s => s.Contains("تاریخ دریافت") || s.Contains("تاریخ تهیه"), preferNext: false);

        // Preserve the well-documented legacy TSV positions as a fallback, but prefer label-based extraction.
        if (rows.Count > 0)
        {
            var first = rows[0].Select(CleanCell).ToList();
            var hasLegacyMetadataLayout = first.Count > 7 &&
                (NormalizeText(GetCell(first, 1)).Contains("تا تاریخ") ||
                 NormalizeText(GetCell(first, 3)).Contains("از تاریخ") ||
                 NormalizeText(GetCell(first, 5)).Contains("کد شعبه") ||
                 NormalizeText(GetCell(first, 6)).Contains("شبا"));
            if (hasLegacyMetadataLayout)
            {
                if (string.IsNullOrWhiteSpace(result.ReportTo)) result.ReportTo = GetCell(first, 0);
                if (string.IsNullOrWhiteSpace(result.ReportFrom)) result.ReportFrom = GetCell(first, 2);
                if (string.IsNullOrWhiteSpace(result.BranchCode)) result.BranchCode = GetCell(first, 4);
                if (string.IsNullOrWhiteSpace(result.Iban) && first.Count > 7) result.Iban = NormalizeIban(GetCell(first, 7));
                if (string.IsNullOrWhiteSpace(result.ReceivedDate) && first.Count > 8) result.ReceivedDate = GetCell(first, 8);
            }
        }

        for (var r = 0; r < scanLimit; r++)
        {
            foreach (var rawCell in rows[r])
            {
                var cell = CleanCell(rawCell);
                var normalized = NormalizeText(cell);
                if (cell.Length == 0 || normalized.Contains("شبا") || normalized.Contains("توضیحات") || normalized.Contains("نام و نام")) continue;
                if (normalized.StartsWith("جناب", StringComparison.Ordinal) || normalized.StartsWith("شرکت", StringComparison.Ordinal) || normalized.StartsWith("سرکار", StringComparison.Ordinal) || normalized.Contains("آقا") || normalized.Contains("خانم"))
                {
                    result.CustomerName = cell;
                    break;
                }
            }
            if (!string.IsNullOrWhiteSpace(result.CustomerName)) break;
        }

        if (string.IsNullOrWhiteSpace(result.CustomerName))
        {
            for (var r = 0; r < Math.Min(rows.Count, tableHeaderIndex); r++)
            {
                var row = rows[r].Select(CleanCell).ToList();
                var labelIndex = row.FindIndex(c => NormalizeText(c).Contains("نام و نام خانوادگی") || NormalizeText(c).Contains("نام صاحب حساب"));
                if (labelIndex >= 0)
                {
                    result.CustomerName = row.Skip(labelIndex + 1).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(result.CustomerName) && r + 1 < rows.Count)
                        result.CustomerName = rows[r + 1].FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty;
                    break;
                }
            }
        }

        result.ReportFrom = NormalizeDate(result.ReportFrom);
        result.ReportTo = NormalizeDate(result.ReportTo);
        result.ReceivedDate = NormalizeDate(result.ReceivedDate);
        result.Iban = NormalizeIban(result.Iban);
        return result;
    }

    private static string FindLabeledValue(List<List<string>> rows, int scanLimit, Func<string, bool> labelTest, bool preferNext)
    {
        for (var r = 0; r < scanLimit; r++)
        {
            var row = rows[r].Select(CleanCell).ToList();
            for (var c = 0; c < row.Count; c++)
            {
                if (!labelTest(NormalizeText(row[c]))) continue;
                var first = preferNext ? c + 1 : c - 1;
                var second = preferNext ? c - 1 : c + 1;
                foreach (var index in new[] { first, second })
                {
                    if (index < 0 || index >= row.Count) continue;
                    var value = row[index];
                    if (string.IsNullOrWhiteSpace(value)) continue;
                    var n = NormalizeText(value);
                    if (n.Contains("تاریخ") || n.Contains("کد شعبه") || n.Contains("شماره حساب شبا") || n == "شبا") continue;
                    return value;
                }
            }
        }
        return string.Empty;
    }

    private static bool StartsWithBreakTag(string description) => Regex.IsMatch(description ?? string.Empty, @"^\s*<br\s*/?>", RegexOptions.IgnoreCase);

    private static decimal ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0m;
        var value = ToWesternDigits(raw.Trim()).Replace("\u00A0", string.Empty).Replace("٬", string.Empty).Replace("،", string.Empty);
        var isNegative = (value.StartsWith('(') && value.EndsWith(')')) || value.StartsWith('-');
        var filtered = new string(value.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray()).Replace(",", string.Empty);
        var dots = filtered.Count(c => c == '.');
        if (dots > 1)
        {
            var lastDot = filtered.LastIndexOf('.');
            var decimals = filtered.Length - lastDot - 1;
            filtered = decimals is 1 or 2 ? filtered[..lastDot].Replace(".", string.Empty, StringComparison.Ordinal) + filtered[lastDot..] : filtered.Replace(".", string.Empty, StringComparison.Ordinal);
        }
        else if (dots == 1)
        {
            var dot = filtered.IndexOf('.');
            if (filtered.Length - dot - 1 == 3) filtered = filtered.Replace(".", string.Empty, StringComparison.Ordinal);
        }
        if (!decimal.TryParse(filtered, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount)) return 0m;
        return isNegative ? -Math.Abs(amount) : amount;
    }

    private static bool TryParseInt(string? raw, out int number)
    {
        var digits = ToWesternDigits(raw ?? string.Empty);
        var numeric = new string(digits.Where(c => char.IsDigit(c) || c == '-').ToArray());
        return int.TryParse(numeric, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
    }

    public static string NormalizeDate(string? raw)
    {
        var value = ToWesternDigits(CleanCell(raw)).Trim().Replace('-', '/').Replace('.', '/');
        var match = Regex.Match(value, @"^(\d{4})/(\d{1,2})/(\d{1,2})$");
        if (match.Success)
            return $"{match.Groups[1].Value}/{match.Groups[2].Value.PadLeft(2, '0')}/{match.Groups[3].Value.PadLeft(2, '0')}";
        return value;
    }

    private static string NormalizeTime(string? raw)
    {
        var value = ToWesternDigits(CleanCell(raw));
        var match = Regex.Match(value, @"^(\d{1,2}):(\d{2})(?::(\d{2}))?$");
        if (!match.Success) return value;
        return $"{match.Groups[1].Value.PadLeft(2, '0')}:{match.Groups[2].Value}" +
               (match.Groups[3].Success ? $":{match.Groups[3].Value}" : string.Empty);
    }

    private static string NormalizeIban(string? raw) => Regex.Replace(CleanCell(raw).ToUpperInvariant(), @"\s+", string.Empty);

    public static string NormalizeText(string? raw) => TextNormalizer.Normalize(CleanCell(raw));

    private static string ToWesternDigits(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= '۰' and <= '۹') chars[i] = (char)('0' + chars[i] - '۰');
            else if (chars[i] is >= '٠' and <= '٩') chars[i] = (char)('0' + chars[i] - '٠');
        }
        return new string(chars);
    }

    private static string GetCell(IReadOnlyList<string> cells, int index) => index >= 0 && index < cells.Count ? CleanCell(cells[index]) : string.Empty;

    private static string BuildSample(List<List<string>> rows)
    {
        var lines = rows.Take(10).Select((row, index) => $"{index + 1}: {string.Join(" | ", row.Select(CleanCell))}");
        var sample = string.Join(Environment.NewLine, lines);
        return string.IsNullOrWhiteSpace(sample) ? "(هیچ ردیفی خوانده نشد)" : sample;
    }

    private sealed record SheetRows(string Name, List<List<string>> Rows);
    private sealed record HeaderMatch(int RowIndex, ColumnMap Columns, int Score);

    private sealed class ColumnMap
    {
        public int Iban { get; set; } = -1;
        public int Description { get; set; } = -1;
        public int TrackingDeposit { get; set; } = -1;
        public int Identifier { get; set; } = -1;
        public int Counterparty { get; set; } = -1;
        public int Balance { get; set; } = -1;
        public int Deposit { get; set; } = -1;
        public int Withdrawal { get; set; } = -1;
        public int BranchCode { get; set; } = -1;
        public int Tracking { get; set; } = -1;
        public int Time { get; set; } = -1;
        public int Date { get; set; } = -1;
        public int RowNumber { get; set; } = -1;
    }
}
