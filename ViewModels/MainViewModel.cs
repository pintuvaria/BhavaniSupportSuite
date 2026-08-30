using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;

namespace BhavaniSupportSuite.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DiagnosticsService _diagnosticsService;
    private readonly NetworkScanner _networkScanner;

    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private string _selectedNavItem;

    [ObservableProperty]
    private bool _isSidebarExpanded;

    [ObservableProperty]
    private string _systemStatus;

    [ObservableProperty]
    private float _cpuUsage;

    [ObservableProperty]
    private float _memoryUsage;

    [ObservableProperty]
    private float _cpuTemperature;

    [ObservableProperty]
    private float _cpuTemperatureF;

    [ObservableProperty]
    private string _currentTime;

    [ObservableProperty]
    private bool _isWelcomeActive;

    [ObservableProperty]
    private bool _isGoodbyeActive;

    public ObservableCollection<NavItem> NavItems { get; } = new()
    {
        new NavItem { Icon = "\uE80F", Label = "Dashboard", ViewName = "Dashboard" },
        new NavItem { Icon = "\uE9A7", Label = "Hardware", ViewName = "Hardware" },
        new NavItem { Icon = "\uE777", Label = "Provisioning", ViewName = "Staging" },
        new NavItem { Icon = "\uE968", Label = "Network", ViewName = "Network" },
        new NavItem { Icon = "\uE730", Label = "Security", ViewName = "Security" },
        new NavItem { Icon = "\uE8D1", Label = "Vault", ViewName = "Vault" },
        new NavItem { Icon = "\uE74E", Label = "Storage", ViewName = "Storage" },
        new NavItem { Icon = "\uE774", Label = "Reports", ViewName = "Reports" }
    };

    public DashboardViewModel DashboardVM { get; }
    public HardwareViewModel HardwareVM { get; }
    public StagingViewModel StagingVM { get; }
    public NetworkViewModel NetworkVM { get; }
    public SecurityViewModel SecurityVM { get; }
    public VaultViewModel VaultVM { get; }
    public StorageViewModel StorageVM { get; }
    public ReportsViewModel ReportsVM { get; }

    public static IValueConverter SidebarWidthConverter { get; } = new SidebarWidthValueConverter();
    public static IValueConverter SidebarToggleConverter { get; } = new SidebarToggleValueConverter();

    public MainViewModel()
    {
        _diagnosticsService = new DiagnosticsService();
        _networkScanner = new NetworkScanner();
        _currentView = null!;
        _selectedNavItem = "Dashboard";
        _isSidebarExpanded = true;
        _systemStatus = "Ready";
        _currentTime = DateTime.Now.ToString("HH:mm:ss");
        _isWelcomeActive = true;
        _isGoodbyeActive = false;

        DashboardVM = new DashboardViewModel(_diagnosticsService);
        HardwareVM = new HardwareViewModel();
        StagingVM = new StagingViewModel(_diagnosticsService);
        NetworkVM = new NetworkViewModel(_networkScanner, _diagnosticsService);
        SecurityVM = new SecurityViewModel(_diagnosticsService);
        VaultVM = new VaultViewModel();
        StorageVM = new StorageViewModel();
        ReportsVM = new ReportsViewModel();

        _currentView = DashboardVM;

        _ = StartPerformanceCountersAsync();
        _ = UpdateClockAsync();
        _ = ShowWelcomeScreenAsync();
    }

    private async Task ShowWelcomeScreenAsync()
    {
        await Task.Delay(3000);
        IsWelcomeActive = false;
    }

    [RelayCommand]
    private void Navigate(string viewName)
    {
        CurrentView = viewName switch
        {
            "Dashboard" => DashboardVM,
            "Hardware" => HardwareVM,
            "Staging" => StagingVM,
            "Network" => NetworkVM,
            "Security" => SecurityVM,
            "Vault" => VaultVM,
            "Storage" => StorageVM,
            "Reports" => ReportsVM,
            _ => DashboardVM
        };
        SelectedNavItem = viewName;
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
    }

    public void ShowGoodbye()
    {
        IsGoodbyeActive = true;
        IsWelcomeActive = false;
    }

    private async Task StartPerformanceCountersAsync()
    {
        while (true)
        {
            try
            {
                CpuUsage = DiagnosticsService.GetCpuUsage();
                MemoryUsage = DiagnosticsService.GetMemoryUsage();
                CpuTemperature = DiagnosticsService.GetCpuTemperature();
                CpuTemperatureF = CpuTemperature * 9f / 5f + 32f;
            }
            catch { }
            await Task.Delay(2000);
        }
    }

    private async Task UpdateClockAsync()
    {
        while (true)
        {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            await Task.Delay(1000);
        }
    }
}

public class NavItem
{
    public string Icon { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ViewName { get; set; } = string.Empty;
}

public class SidebarWidthValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? 220.0 : 68.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SidebarToggleValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return "\uE700";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
