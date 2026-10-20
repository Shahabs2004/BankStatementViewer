using ClosedXML.Excel;
using BankStatementViewer.Models;
using System.Globalization;

namespace BankStatementViewer.Services;

public sealed class ExcelExportService
{
    public void Export(
        string path,
        StatementHeader header,
        IReadOnlyCollection<StatementTransaction> transactions,
        IReadOnlyCollection<string> selectedColumnKeys,
        IProgress<int>? progress = null)
    {
        var columns = TransactionColumnCatalog.Resolve(selectedColumnKeys);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("گردش حساب");
        worksheet.RightToLeft = true;
        var columnCount = columns.Count;

        var titleRange = worksheet.Range(1, 1, 1, columnCount);
        if (columnCount > 1) titleRange.Merge();
        worksheet.Cell(1, 1).Value = "صورت‌حساب گردش حساب بانکی";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE8EF");
        worksheet.Row(1).Height = 28;

        WriteInfoRow(worksheet, 2, columnCount,
            $"نام صاحب حساب: {Dash(header.CustomerName)}     شبا: {Dash(header.Iban)}");
        WriteInfoRow(worksheet, 3, columnCount,
            $"کد شعبه: {Dash(header.BranchCode)}     تاریخ دریافت: {Dash(header.ReceivedDate)}");
        WriteInfoRow(worksheet, 4, columnCount,
            $"بازه گزارش: {Dash(header.ReportPeriod)}     فایل: {Dash(header.SourceFileName)}");
        WriteInfoRow(worksheet, 5, columnCount,
            $"مانده آغازین: {header.OpeningBalance.ToString("N0", CultureInfo.InvariantCulture)}     مانده پایانی: {header.ClosingBalance.ToString("N0", CultureInfo.InvariantCulture)}");

        const int headerRow = 7;
        for (var c = 0; c < columnCount; c++)
            worksheet.Cell(headerRow, c + 1).Value = columns[c].Header;

        var headerRange = worksheet.Range(headerRow, 1, headerRow, columnCount);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E7EB");
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        worksheet.Row(headerRow).Height = 32;

        var row = headerRow + 1;
        var transactionCount = transactions.Count;
        var processed = 0;
        progress?.Report(0);
        foreach (var transaction in transactions)
        {
            for (var colIndex = 0; colIndex < columns.Count; colIndex++)
            {
                var definition = columns[colIndex];
                var cell = worksheet.Cell(row, colIndex + 1);
                var value = definition.Value(transaction);
                switch (value)
                {
                    case decimal decimalValue:
                        cell.Value = decimalValue;
                        break;
                    case int intValue:
                        cell.Value = intValue;
                        break;
                    case long longValue:
                        cell.Value = longValue;
                        break;
                    default:
                        cell.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                        break;
                }

                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Alignment.WrapText = definition.IsText;
                if (definition.IsAmount)
                {
                    cell.Style.NumberFormat.Format = "#,##0;[Red](#,##0);-";
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
                else if (definition.Key == "RowNumber")
                {
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                if (definition.Key == "TransactionType")
                {
                    if (transaction.IsDeposit) cell.Style.Font.FontColor = XLColor.FromHtml("#16803C");
                    else if (transaction.IsWithdrawal) cell.Style.Font.FontColor = XLColor.FromHtml("#B42318");
                }
            }

            var currentRange = worksheet.Range(row, 1, row, columnCount);
            currentRange.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            if (row % 2 == 0) currentRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
            worksheet.Row(row).Height = EstimateRowHeight(transaction, columns);
            row++;
            processed++;
            if (processed == transactionCount || processed % Math.Max(1, transactionCount / 100) == 0)
                progress?.Report(transactionCount == 0 ? 100 : (int)Math.Round(processed * 100d / transactionCount));
        }

        var lastRow = Math.Max(headerRow, row - 1);
        worksheet.Range(headerRow, 1, lastRow, columnCount).SetAutoFilter();
        worksheet.SheetView.FreezeRows(headerRow);
        worksheet.Style.Font.FontName = "Tahoma";
        worksheet.Style.Font.FontSize = 10;
        worksheet.Style.Alignment.ReadingOrder = XLAlignmentReadingOrderValues.RightToLeft;
        for (var i = 0; i < columns.Count; i++)
            worksheet.Column(i + 1).Width = columns[i].ExcelWidth;

        worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        worksheet.PageSetup.FitToPages(1, 0);
        worksheet.PageSetup.PagesWide = 1;
        worksheet.PageSetup.Margins.Top = 0.4;
        worksheet.PageSetup.Margins.Bottom = 0.4;
        worksheet.PageSetup.Margins.Left = 0.25;
        worksheet.PageSetup.Margins.Right = 0.25;
        progress?.Report(98);
        workbook.SaveAs(path);
        progress?.Report(100);
    }

    private static double EstimateRowHeight(StatementTransaction transaction, IReadOnlyList<TransactionColumnDefinition> columns)
    {
        var estimatedLines = 1;
        foreach (var column in columns)
        {
            var value = Convert.ToString(column.Value(transaction), CultureInfo.InvariantCulture) ?? string.Empty;
            var widthInChars = Math.Max(8, column.ExcelWidth * 1.15);
            foreach (var line in value.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
                estimatedLines = Math.Max(estimatedLines, (int)Math.Ceiling(Math.Max(1, line.Length) / widthInChars));
        }
        // Avoid the expensive workbook-wide layout measure for every row; still expose multi-line values.
        return Math.Clamp(estimatedLines * 15d + 6d, 24d, 120d);
    }

    private static void WriteInfoRow(IXLWorksheet worksheet, int row, int columnCount, string text)
    {
        var range = worksheet.Range(row, 1, row, columnCount);
        if (columnCount > 1) range.Merge();
        worksheet.Cell(row, 1).Value = text;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        range.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
        worksheet.Row(row).Height = 21;
    }

    private static string Dash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
}
