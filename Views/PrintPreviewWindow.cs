using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace BankStatementViewer.Views;

public sealed class PrintPreviewWindow : Window
{
    public PrintPreviewWindow(FixedDocument document)
    {
        Title = "پیش‌نمایش چاپ";
        Width = 1050;
        Height = 760;
        MinWidth = 720;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FlowDirection = FlowDirection.RightToLeft;

        var root = new DockPanel();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(12) };
        var print = new Button { Content = "چاپ", MinWidth = 100, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        print.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = "لغو", MinWidth = 100, Padding = new Thickness(12, 6, 12, 6), IsCancel = true };
        buttons.Children.Add(print);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        root.Children.Add(new DocumentViewer { Document = document, Zoom = 100, Background = new SolidColorBrush(Color.FromRgb(235, 238, 241)) });
        Content = root;
    }
}
