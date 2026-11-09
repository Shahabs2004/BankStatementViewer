using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Threading.Tasks;
using BankStatementViewer.Models;

namespace BankStatementViewer.Services;

/// <summary>
/// Builds a fixed-layout print document. Fixed column widths and explicit pagination
/// prevent long transaction descriptions from expanding the table beyond the page.
/// </summary>
public sealed class PrintService
{
    private static readonly FontFamily PrintFont = new("Tahoma");
    private static readonly Brush BorderBrush = new SolidColorBrush(Color.FromRgb(184, 194, 202));
    private static readonly Brush HeaderBrush = new SolidColorBrush(Color.FromRgb(225, 231, 236));
    private static readonly Brush AlternateBrush = new SolidColorBrush(Color.FromRgb(248, 250, 252));

    static PrintService()
    {
        // Freeze brushes (Freezable objects) for safe reuse.
        // FontFamily does not derive from Freezable and cannot be frozen.
        BorderBrush.Freeze();
        HeaderBrush.Freeze();
        AlternateBrush.Freeze();
    }

    /// <summary>
    /// Creates the WPF print document and shows the printer dialog on the calling
    /// UI thread. WPF visual/document objects have thread affinity and must not be
    /// created on a worker thread then accessed by the UI/printer infrastructure.
    /// </summary>
    public Task<bool> PrintAsync(
        StatementHeader header,
        IReadOnlyCollection<StatementTransaction> transactions,
        bool landscape,
        IReadOnlyCollection<string> selectedColumnKeys,
        AppSettings settings)
    {
        // Keep all WPF PrintDialog, FixedDocument, FixedPage, Grid and TextBlock
        // creation/use on the same dispatcher thread to avoid cross-thread access.
        var result = PrintCore(header, transactions.ToList(), landscape, selectedColumnKeys.ToList(), settings);
        return Task.FromResult(result);
    }

    private static bool PrintCore(
        StatementHeader header,
        IReadOnlyCollection<StatementTransaction> transactions,
        bool landscape,
        IReadOnlyCollection<string> selectedColumnKeys,
        AppSettings settings)
    {
        var printOrder = new[]
        {
            "RowNumber", "Date", "Time", "EntryKind", "Description", "Deposit", "Withdrawal", "Balance",
            "BranchCode", "Tracking", "Channel", "OtherAccount", "Purpose", "TransactionType",
            "Iban", "Identifier", "TrackingDeposit", "Counterparty", "FullDescription"
        };
        var columns = TransactionColumnCatalog.Resolve(selectedColumnKeys)
            .OrderBy(c => Array.IndexOf(printOrder, c.Key)).ToList();
        var dialog = new PrintDialog();
        try
        {
            dialog.PrintTicket = new PrintTicket
            {
                PageOrientation = landscape ? PageOrientation.Landscape : PageOrientation.Portrait
            };
        }
        catch (PrintQueueException)
        {
            // Some printer drivers only accept orientation after a printer is selected.
        }

        if (dialog.ShowDialog() != true) return false;

        var pageWidth = dialog.PrintableAreaWidth;
        var pageHeight = dialog.PrintableAreaHeight;
        if (landscape && pageWidth < pageHeight) (pageWidth, pageHeight) = (pageHeight, pageWidth);
        if (!landscape && pageWidth > pageHeight) (pageWidth, pageHeight) = (pageHeight, pageWidth);
        if (pageWidth <= 0 || pageHeight <= 0)
            throw new InvalidOperationException("ابعاد قابل چاپ از درایور چاپگر دریافت نشد.");

        var document = CreateDocument(header, transactions.ToList(), columns, pageWidth, pageHeight, landscape, settings);
        dialog.PrintDocument(document.DocumentPaginator, "گزارش گردش حساب بانکی");
        return true;
    }

    private static FixedDocument CreateDocument(
        StatementHeader header,
        IReadOnlyList<StatementTransaction> transactions,
        IReadOnlyList<TransactionColumnDefinition> columns,
        double pageWidth,
        double pageHeight,
        bool landscape,
        AppSettings settings)
    {
        const double margin = 16;
        const double headerY = 190;
        const double tableHeaderHeight = 27;
        const double footerReserve = 28;
        var innerWidth = Math.Max(100, pageWidth - 2 * margin);
        var columnWidths = ComputeColumnWidths(columns, innerWidth);
        var rowStartY = headerY + tableHeaderHeight;
        var footerY = pageHeight - footerReserve;
        var bodyHeight = Math.Max(100, footerY - rowStartY - 2);
        var fontSize = landscape ? 8.6 : 8.3;

        var measuredRows = transactions.Select(t => new MeasuredRow(t, MeasureRowHeight(t, columns, columnWidths, fontSize, bodyHeight))).ToList();
        var pages = PaginateRows(measuredRows, bodyHeight);

        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var page = new FixedPage
            {
                Width = pageWidth,
                Height = pageHeight,
                Background = Brushes.White,
                ClipToBounds = true
            };

            AddAt(page, CreateTextBlock("گزارش گردش حساب بانکی", innerWidth, 23, 14, FontWeights.Bold,
                TextAlignment.Center, FlowDirection.RightToLeft), margin, 8);

            var logoStream = Application.GetResourceStream(new Uri("pack://application:,,,/Agri-bank_logo.png"));
            const double branchInfoWidth = 260;
            const double logoWidth = 81;
            const double logoHeight = 93;
            var branchInfoX = margin + innerWidth - branchInfoWidth;
            if (logoStream is not null)
            {
                using (logoStream.Stream)
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = logoStream.Stream; bitmap.DecodePixelHeight = 108; bitmap.EndInit(); bitmap.Freeze();
                    var logo = new Image { Source = bitmap, Width = logoWidth, Height = logoHeight, Stretch = Stretch.Uniform };
                    AddAt(page, logo, branchInfoX + (branchInfoWidth - logoWidth) / 2, 3);
                }
            }

            var provinceLine = $"مدیریت بانک کشاورزی استان {Dash(settings.ProvinceName)}";
            var branchLine = $"شعبه {Dash(settings.BranchName)} ـ کد {Dash(settings.PrintBranchCode)}";
            var printDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.GetCultureInfo("fa-IR"));
            AddAt(page, CreateTextBlock(provinceLine, branchInfoWidth, 17, 9.2, FontWeights.Bold, TextAlignment.Center, FlowDirection.RightToLeft), branchInfoX, 99);
            AddAt(page, CreateTextBlock(branchLine, branchInfoWidth, 17, 9.0, FontWeights.Bold, TextAlignment.Center, FlowDirection.RightToLeft), branchInfoX, 116);
            AddAt(page, CreateTextBlock($"تاریخ چاپ: {printDate}", innerWidth - branchInfoWidth - 8, 15, 7.8, FontWeights.Normal, TextAlignment.Left, FlowDirection.RightToLeft), margin, 70);

            var line1 = $"صاحب حساب: {Dash(header.CustomerName)}     شبا: {Dash(header.Iban)}";
            var line2 = $"بازه گزارش: {Dash(header.ReportPeriod)}     کد شعبه صورت‌حساب: {Dash(header.BranchCode)}     تاریخ دریافت: {Dash(header.ReceivedDate)}";
            var line3 = $"مانده آغازین: {header.OpeningBalance.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}     مانده پایانی: {header.ClosingBalance.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}     تعداد رکوردها: {transactions.Count:N0}";
            AddAt(page, CreateTextBlock(line1, innerWidth, 16, 7.8, FontWeights.Normal, TextAlignment.Right, FlowDirection.RightToLeft), margin, 138);
            AddAt(page, CreateTextBlock(line2, innerWidth, 16, 7.8, FontWeights.Normal, TextAlignment.Right, FlowDirection.RightToLeft), margin, 154);
            AddAt(page, CreateTextBlock(line3, innerWidth, 16, 7.8, FontWeights.Normal, TextAlignment.Right, FlowDirection.RightToLeft), margin, 170);

            var tableHeader = CreateGridRow(columns.Select(c => c.Header).ToList(), columns, columnWidths,
                tableHeaderHeight, 8.5, true, false);
            AddAt(page, tableHeader, margin, headerY);

            var currentY = rowStartY;
            foreach (var rowIndex in pages[pageIndex])
            {
                var row = measuredRows[rowIndex];
                var rowElement = CreateGridRow(
                    columns.Select(c => c.DisplayText(row.Transaction)).ToList(), columns, columnWidths,
                    row.Height, fontSize, false, rowIndex % 2 == 1);
                AddAt(page, rowElement, margin, currentY);
                currentY += row.Height;
            }

            var footer = CreateTextBlock(
                $"تعداد رکوردها: {transactions.Count:N0}     صفحه {pageIndex + 1:N0} از {pages.Count:N0}",
                innerWidth, 18, 8.0, FontWeights.Bold, TextAlignment.Left, FlowDirection.RightToLeft);
            AddAt(page, footer, margin, footerY);

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(page);
            document.Pages.Add(pageContent);
        }

        return document;
    }

    private static double[] ComputeColumnWidths(IReadOnlyList<TransactionColumnDefinition> columns, double availableWidth)
    {
        var minimums = columns.Select(c => c.MinimumPrintWidth).ToArray();
        var minimumTotal = minimums.Sum();
        if (minimumTotal >= availableWidth)
        {
            // On narrow portrait pages with many selected fields, scale down together
            // rather than letting any one column force the table outside the page.
            var factor = availableWidth / minimumTotal;
            for (var i = 0; i < minimums.Length; i++) minimums[i] *= factor;
            return CorrectLastWidth(minimums, availableWidth);
        }

        var remaining = availableWidth - minimumTotal;
        var totalWeight = columns.Sum(c => Math.Max(0.1, c.PrintWeight));
        var widths = columns.Select((c, i) => minimums[i] + remaining * Math.Max(0.1, c.PrintWeight) / totalWeight).ToArray();
        return CorrectLastWidth(widths, availableWidth);
    }

    private static double[] CorrectLastWidth(double[] widths, double total)
    {
        if (widths.Length > 0) widths[^1] += total - widths.Sum();
        return widths;
    }

    private static double MeasureRowHeight(
        StatementTransaction transaction,
        IReadOnlyList<TransactionColumnDefinition> columns,
        IReadOnlyList<double> widths,
        double fontSize,
        double maximumHeight)
    {
        var measuredHeight = 18d;
        for (var i = 0; i < columns.Count; i++)
        {
            var availableWidth = Math.Max(8, widths[i] - 8);
            if (columns[i].Key == "Description")
            {
                var description = CreateDescriptionPanel(columns[i].DisplayText(transaction), availableWidth, fontSize);
                description.Measure(new Size(availableWidth, double.PositiveInfinity));
                measuredHeight = Math.Max(measuredHeight, description.DesiredSize.Height + 6);
                continue;
            }

            var text = TransactionColumnDefinition.MakeBreakable(columns[i].DisplayText(transaction));
            var block = CreateTextBlock(text, availableWidth, double.NaN, fontSize,
                FontWeights.Normal, AlignmentFor(columns[i]), FlowDirectionFor(columns[i]));
            block.TextWrapping = TextWrapping.Wrap;
            block.Measure(new Size(availableWidth, double.PositiveInfinity));
            measuredHeight = Math.Max(measuredHeight, block.DesiredSize.Height + 6);
        }
        return Math.Min(Math.Max(18, Math.Ceiling(measuredHeight)), maximumHeight);
    }

    private static List<List<int>> PaginateRows(IReadOnlyList<MeasuredRow> rows, double bodyHeight)
    {
        var pages = new List<List<int>>();
        var currentPage = new List<int>();
        var usedHeight = 0d;

        for (var i = 0; i < rows.Count; i++)
        {
            var rowHeight = Math.Min(rows[i].Height, bodyHeight);
            if (currentPage.Count > 0 && usedHeight + rowHeight > bodyHeight)
            {
                pages.Add(currentPage);
                currentPage = new List<int>();
                usedHeight = 0;
            }
            currentPage.Add(i);
            usedHeight += rowHeight;
        }

        if (currentPage.Count > 0 || pages.Count == 0) pages.Add(currentPage);
        return pages;
    }

    private static Grid CreateGridRow(
        IReadOnlyList<string> values,
        IReadOnlyList<TransactionColumnDefinition> columns,
        IReadOnlyList<double> widths,
        double height,
        double fontSize,
        bool isHeader,
        bool alternate)
    {
        var totalWidth = widths.Sum();
        var grid = new Grid
        {
            Width = totalWidth,
            Height = height,
            FlowDirection = FlowDirection.RightToLeft,
            ClipToBounds = true
        };
        foreach (var width in widths)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Pixel) });

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            UIElement cellContent;
            if (!isHeader && column.Key == "Description")
            {
                cellContent = CreateDescriptionPanel(values[i], Math.Max(8, widths[i] - 8), fontSize);
            }
            else
            {
                var value = isHeader ? values[i] : TransactionColumnDefinition.MakeBreakable(values[i]);
                var text = CreateTextBlock(value, Math.Max(8, widths[i] - 8), double.NaN, fontSize,
                    isHeader ? FontWeights.Bold : FontWeights.Normal,
                    isHeader ? TextAlignment.Center : AlignmentFor(column),
                    isHeader ? FlowDirection.RightToLeft : FlowDirectionFor(column));
                text.TextWrapping = TextWrapping.Wrap;
                text.VerticalAlignment = VerticalAlignment.Center;
                text.ClipToBounds = true;
                cellContent = text;
            }

            var border = new Border
            {
                BorderBrush = BorderBrush,
                BorderThickness = new Thickness(0.35),
                Padding = new Thickness(3, 1, 3, 1),
                Background = isHeader ? HeaderBrush : alternate ? AlternateBrush : Brushes.White,
                Child = cellContent,
                ClipToBounds = true
            };
            Grid.SetColumn(border, i);
            grid.Children.Add(border);
        }
        return grid;
    }

    private static TextBlock CreateTextBlock(
        string text,
        double width,
        double height,
        double fontSize,
        FontWeight weight,
        TextAlignment alignment,
        FlowDirection flowDirection)
    {
        var block = new TextBlock
        {
            Text = text ?? string.Empty,
            Width = width,
            FontFamily = PrintFont,
            FontSize = fontSize,
            FontWeight = weight,
            TextAlignment = alignment,
            FlowDirection = flowDirection,
            TextWrapping = TextWrapping.NoWrap,
            Foreground = Brushes.Black,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!double.IsNaN(height) && height > 0) block.Height = height;
        return block;
    }

    /// <summary>
    /// Renders each description line as a two-column row: Persian labels on the
    /// right and their associated values (usually numbers) on the left.
    /// </summary>
    private static StackPanel CreateDescriptionPanel(string? description, double width, double fontSize)
    {
        var panel = new StackPanel
        {
            Width = width,
            FlowDirection = FlowDirection.RightToLeft
        };

        var lines = (description ?? string.Empty)
            .Split(new[] { "\\r\\n", "\\n", "\\r" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var separator = line.IndexOfAny(new[] { ':', '：' });
            var row = new Grid
            {
                Width = width,
                MinHeight = fontSize + 3,
                FlowDirection = FlowDirection.RightToLeft
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42, GridUnitType.Star) });

            var labelText = separator >= 0 ? line.Substring(0, separator).Trim() : line;
            var valueText = separator >= 0 ? line.Substring(separator + 1).Trim() : string.Empty;

            var label = CreateTextBlock(labelText, double.NaN, double.NaN, fontSize,
                FontWeights.Normal, TextAlignment.Right, FlowDirection.RightToLeft);
            label.TextWrapping = TextWrapping.Wrap;
            label.VerticalAlignment = VerticalAlignment.Center;

            var value = CreateTextBlock(valueText, double.NaN, double.NaN, fontSize,
                FontWeights.Normal, TextAlignment.Left, FlowDirection.LeftToRight);
            value.TextWrapping = TextWrapping.Wrap;
            value.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetColumn(label, 0);
            Grid.SetColumn(value, 1);
            row.Children.Add(label);
            row.Children.Add(value);
            panel.Children.Add(row);
        }

        return panel;
    }

    private static TextAlignment AlignmentFor(TransactionColumnDefinition column) =>
        column.Key == "Description"
            ? TextAlignment.Left // With RTL FlowDirection, WPF mirrors the cell's layout coordinates.
            : column.IsText
                ? TextAlignment.Right
                : TextAlignment.Center;

    private static FlowDirection FlowDirectionFor(TransactionColumnDefinition column) =>
        column.Key == "Description" || column.IsText || column.Key == "TransactionType"
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

    private static void AddAt(FixedPage page, UIElement element, double left, double top)
    {
        FixedPage.SetLeft(element, left);
        FixedPage.SetTop(element, top);
        page.Children.Add(element);
    }

    private static string Dash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private sealed record MeasuredRow(StatementTransaction Transaction, double Height);
}
