using System.Windows;
using System.Threading.Tasks;

namespace BhavaniSupportSuite;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"An unexpected error occurred:\n\n{args.Exception}",
                "Bhavani Support Suite Pro", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show($"Fatal error:\n\n{ex}",
                    "Bhavani Support Suite Pro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }
}
