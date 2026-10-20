using System.Windows;

namespace BankStatementViewer.Views;

public partial class InputDialog : Window
{
    public InputDialog() => InitializeComponent();

    public string PresetName => NameTextBox.Text.Trim();
    public string SearchText => SearchTextBox.Text.Trim();

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PresetName) || string.IsNullOrWhiteSpace(SearchText))
        {
            MessageBox.Show(this, "نام و عبارت جستجو را وارد کنید.", "ورودی ناقص", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
