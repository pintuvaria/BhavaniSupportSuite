using System.Windows;

namespace BhavaniSupportSuite;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"An unexpected error occurred:\n\n{args.Exception.Message}",
                "Bhavani Support Suite Pro", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
