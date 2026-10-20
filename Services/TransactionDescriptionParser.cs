using System.Text.RegularExpressions;
using BankStatementViewer.Models;

namespace BankStatementViewer.Services;

/// <summary>Extracts useful customer-facing details from bank-specific multi-line descriptions.</summary>
public static class TransactionDescriptionParser
{
    private static readonly Regex BreakTag = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PurposeLabel = new(
        @"^\s*(?:بابت|موضوع|علت|هدف|کاربرد|شرح(?:\s+تراکنش)?|توضیحات|purpose|reason)\s*[:：-]?\s*(.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AccountLabel = new(
        @"^\s*(?:کارت\s*بانک\s*[^:：]*|شماره\s*(?:حساب|کارت)|حساب\s*(?:مقصد|گیرنده)|شماره\s*شبا|شبا|iban|account(?:\s*number)?|card(?:\s*number)?)\s*[:：]\s*(.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AccountAfterVerb = new(
        @"(?:به\s*حساب|از\s*حساب|حساب\s*مقصد|حساب\s*گیرنده)\s*[:：]?\s*([0-9۰-۹٠-٩][0-9۰-۹٠-٩\s-]{7,28})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void Populate(StatementTransaction transaction, string? rawDescription)
    {
        var raw = BreakTag.Replace(rawDescription ?? string.Empty, Environment.NewLine).Trim();
        transaction.RawDescription = raw;
        var lines = raw.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        var firstLine = lines.FirstOrDefault() ?? string.Empty;
        var compactAll = TextNormalizer.Normalize(raw, removeWhitespace: true);

        transaction.Channel = DetectChannel(compactAll);
        transaction.OtherAccount = ExtractOtherAccounts(lines);
        transaction.Purpose = ExtractPurpose(lines) ?? string.Empty;

        // The customer-facing Description column must retain all bank-provided lines.
        // Keep the cleaner operation label on the first non-empty line, followed by all
        // original detail lines, with line breaks preserved for WPF wrapping.
        var displayLines = raw.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select(line => line.Trim())
            .ToList();
        var firstNonEmptyIndex = displayLines.FindIndex(line => line.Length > 0);
        if (firstNonEmptyIndex >= 0)
            displayLines[firstNonEmptyIndex] = CleanOperation(displayLines[firstNonEmptyIndex]);
        transaction.Description = string.Join(Environment.NewLine, displayLines);
        if (compactAll.Contains("کارمزد", StringComparison.Ordinal) || compactAll.Contains("fee", StringComparison.Ordinal))
            transaction.IsFee = true;
    }

    private static string DetectChannel(string compactText)
    {
        if (compactText.Contains("kyos", StringComparison.Ordinal)) return "KYOS";
        if (compactText.Contains("mob", StringComparison.Ordinal)) return "MOB";
        if (compactText.Contains("paya", StringComparison.Ordinal) || compactText.Contains("payment", StringComparison.Ordinal) ||
            compactText.Contains("پایا", StringComparison.Ordinal)) return "PAYA";
        if (compactText.Contains("pos", StringComparison.Ordinal) || compactText.Contains("خریدباpos", StringComparison.Ordinal) ||
            compactText.Contains("خریداینترنتی", StringComparison.Ordinal) || compactText.Contains("شتاب", StringComparison.Ordinal)) return "POS";
        return "سایر";
    }

    private static string? ExtractPurpose(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = PurposeLabel.Match(TextNormalizer.Normalize(line));
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                return match.Groups[1].Value.Trim().Trim(' ', '-', ':', '：', '،', ',');
        }
        return null;
    }

    private static string ExtractOtherAccounts(IReadOnlyList<string> lines)
    {
        var found = new List<string>();
        var cardBankNumbers = new List<string>();

        // In common card-transfer descriptions, the first "کارت بانک" line is the source
        // card, while subsequent card lines identify the destination/other card. Do not
        // incorrectly present a single POS source card as the counterparty account.
        foreach (var line in lines)
        {
            var normalized = TextNormalizer.Normalize(line);
            if (normalized.Contains("پیگیری") || normalized.Contains("سوئیچ") || normalized.Contains("پایانه") ||
                normalized.Contains("ترمینال") || normalized.Contains("شناسه کارتی") || normalized.Contains("کد شعبه")) continue;

            if (normalized.StartsWith("کارت بانک", StringComparison.Ordinal))
            {
                var cardMatch = AccountLabel.Match(normalized);
                if (cardMatch.Success)
                {
                    var cardValue = ExtractDigitsOrIban(cardMatch.Groups[1].Value);
                    if (cardValue.Length >= 8) cardBankNumbers.Add(cardValue);
                }
                continue;
            }

            var match = AccountLabel.Match(normalized);
            if (match.Success)
            {
                var value = ExtractDigitsOrIban(match.Groups[1].Value);
                if (value.Length >= 8) found.Add(value);
                continue;
            }

            match = AccountAfterVerb.Match(normalized);
            if (match.Success)
            {
                var value = ExtractDigitsOrIban(match.Groups[1].Value);
                if (value.Length >= 8) found.Add(value);
            }
        }

        if (cardBankNumbers.Count > 1) found.AddRange(cardBankNumbers.Skip(1));
        return string.Join("، ", found.Distinct(StringComparer.Ordinal));
    }

    private static string ExtractDigitsOrIban(string value)
    {
        var cleaned = value.Trim().TrimEnd('.', ',', ';');
        if (TextNormalizer.Normalize(cleaned).StartsWith("ir", StringComparison.Ordinal))
            return Regex.Replace(cleaned.ToUpperInvariant(), @"\s+", string.Empty);
        var digits = new string(cleaned.Where(char.IsDigit).ToArray());
        return digits.Length >= 8 ? digits : string.Empty;
    }

    private static string CleanOperation(string firstLine)
    {
        if (string.IsNullOrWhiteSpace(firstLine)) return string.Empty;
        var compact = TextNormalizer.Normalize(firstLine, removeWhitespace: true);
        if (compact.Contains("کارمزدپل", StringComparison.Ordinal)) return "کارمزد پل";
        if (compact.Contains("برداشتپل", StringComparison.Ordinal)) return "برداشت پل";
        if (compact.Contains("برداشتنقدی", StringComparison.Ordinal)) return "برداشت نقدی";
        if (compact.Contains("خریدباpos", StringComparison.Ordinal)) return "خرید با کارت (POS)";
        if (compact.Contains("خریداینترنتی", StringComparison.Ordinal)) return "خرید اینترنتی";
        if (compact.Contains("واریزتجمعی", StringComparison.Ordinal)) return "واریز تجمیعی";
        if (compact.Contains("واریزانتقالی", StringComparison.Ordinal)) return "واریز انتقالی";
        if (compact.Contains("کارتهدیه", StringComparison.Ordinal)) return "کارت هدیه";
        if (compact.Contains("mobevoucher", StringComparison.Ordinal)) return "خرید اعتبار موبایل";
        if (compact.StartsWith("انتقالmob", StringComparison.Ordinal) ||
            compact.StartsWith("انتقالpayment", StringComparison.Ordinal) ||
            compact.StartsWith("انتقالpaya", StringComparison.Ordinal) ||
            compact.StartsWith("انتقالkyos", StringComparison.Ordinal) ||
            compact.StartsWith("انتقالپایا", StringComparison.Ordinal)) return "انتقال";

        var cleaned = Regex.Replace(firstLine, @"\b(?:MOB|PAYMENT|PAYA|KYOS|POS)\b", string.Empty, RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(' ', ':', '-', '،', ',');
        return cleaned.Length == 0 ? firstLine.Trim() : cleaned;
    }
}
