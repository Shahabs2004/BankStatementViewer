using System.Windows;
using BankStatementViewer.Services;

namespace BankStatementViewer.Views;

public partial class FtpFilePickerWindow : Window
{
    public FtpStatementFile? SelectedFile => FilesGrid.SelectedItem as FtpStatementFile;
    public FtpFilePickerWindow(IEnumerable<FtpStatementFile> files)
    {
        InitializeComponent();
        FilesGrid.ItemsSource = files;
        FilesGrid.SelectedIndex = 0;
    }
    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFile is null) { MessageBox.Show(this, "یک فایل انتخاب کنید."); return; }
        DialogResult = true;
    }
}
