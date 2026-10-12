using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BhavaniSupportSuite.Core;

public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string _title = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    protected static void SafeDispatch(Action action)
    {
        try
        {
            if (Application.Current?.Dispatcher == null) return;
            if (Application.Current.Dispatcher.CheckAccess())
            {
                try { action(); } catch (Exception ex) { Log.Error(ex, "SafeDispatch UI thread action failed"); }
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    try { action(); } catch (Exception ex) { Log.Error(ex, "SafeDispatch background thread action failed"); }
                });
            }
        }
        catch (Exception ex) { Log.Error(ex, "SafeDispatch failed"); }
    }

    public virtual void Dispose() { }
}
