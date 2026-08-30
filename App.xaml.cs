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
            System.Diagnostics.Debug.WriteLine($"[UI Error] {args.Exception}");
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Task Error] {args.Exception}");
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                System.Diagnostics.Debug.WriteLine($"[Fatal] {ex}");
        };
    }
}
