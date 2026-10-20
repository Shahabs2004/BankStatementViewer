using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace BankStatementViewer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // This is a Persian-language WPF application. The release must keep full globalization data.
        var persianCulture = CultureInfo.GetCultureInfo("fa-IR");
        CultureInfo.DefaultThreadCurrentCulture = persianCulture;
        CultureInfo.DefaultThreadCurrentUICulture = persianCulture;
        Thread.CurrentThread.CurrentCulture = persianCulture;
        Thread.CurrentThread.CurrentUICulture = persianCulture;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"خطای پیش‌بینی‌نشده رخ داد:\n{e.Exception.Message}\n\nجزئیات:\n{e.Exception}",
            "خطای برنامه", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show($"خطای بحرانی برنامه:\n{ex}", "خطای بحرانی", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
    }
}
