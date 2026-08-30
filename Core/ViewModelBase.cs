using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

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
            if (Application.Current?.Dispatcher != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                    action();
                else
                    Application.Current.Dispatcher.BeginInvoke(action);
            }
        }
        catch { }
    }

    public virtual void Dispose() { }
}
