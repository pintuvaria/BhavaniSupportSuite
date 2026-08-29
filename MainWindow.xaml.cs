using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using BhavaniSupportSuite.ViewModels;

namespace BhavaniSupportSuite;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;

        _viewModel?.ShowGoodbye();
        await Task.Delay(2000);
        Application.Current.Shutdown();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing) return;
        e.Cancel = true;
        _isClosing = true;

        _viewModel?.ShowGoodbye();
        await Task.Delay(2000);
        Application.Current.Shutdown();
    }
}
