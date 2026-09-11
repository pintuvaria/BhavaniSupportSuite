using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
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
        _viewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();
        DataContext = _viewModel;

        // Restore window settings
        var settings = App.Settings;
        if (settings.RememberWindowPosition && settings.WindowLeft >= 0 && settings.WindowTop >= 0)
        {
            Left = settings.WindowLeft;
            Top = settings.WindowTop;
        }
        if (settings.WindowWidth > 0 && settings.WindowHeight > 0)
        {
            Width = settings.WindowWidth;
            Height = settings.WindowHeight;
        }

        // Show Welcome screen for 3 seconds, then switch to Main
        ShowWelcomeScreenAsync();
    }

    private async void ShowWelcomeScreenAsync()
    {
        await Task.Delay(3000);
        ShowMainContent();
    }

    private void ShowMainContent()
    {
        WelcomePanel.Visibility = Visibility.Collapsed;
        MainPanel.Visibility = Visibility.Visible;
        GoodbyePanel.Visibility = Visibility.Collapsed;
    }

    private async void ShowGoodbyeScreen()
    {
        GoodbyeTime.Text = DateTime.Now.ToString("hh:mm:ss tt");
        WelcomePanel.Visibility = Visibility.Collapsed;
        MainPanel.Visibility = Visibility.Collapsed;
        GoodbyePanel.Visibility = Visibility.Visible;

        // Save window position
        var settings = App.Settings;
        if (settings.RememberWindowPosition)
        {
            settings.WindowLeft = Left;
            settings.WindowTop = Top;
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
        }

        await Task.Delay(2000);
        Application.Current.Shutdown();
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isClosing) return;
        _isClosing = true;
        ShowGoodbyeScreen();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
        {
            e.Cancel = false;
            return;
        }

        e.Cancel = true;
        _isClosing = true;
        ShowGoodbyeScreen();
    }
}