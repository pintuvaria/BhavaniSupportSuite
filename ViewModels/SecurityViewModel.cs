using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;

namespace BhavaniSupportSuite.ViewModels;

public partial class SecurityViewModel : ViewModelBase
{
    private readonly DiagnosticsService _diagnostics;

    [ObservableProperty]
    private ObservableCollection<ProcessInfo> _runningProcesses = new();

    [ObservableProperty]
    private ObservableCollection<StartupItem> _startupItems = new();

    [ObservableProperty]
    private ObservableCollection<ScheduledTaskInfo> _scheduledTasks = new();

    [ObservableProperty]
    private ObservableCollection<string> _unsignedBinaries = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _usbStorageEnabled;

    [ObservableProperty]
    private bool _bitLockerEnabled;

    [ObservableProperty]
    private string _selectedProcessName = string.Empty;

    public SecurityViewModel(DiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
        Title = "Security Auditor";
        _statusMessage = "Ready";
        _usbStorageEnabled = SecurityService.GetUsbStorageEnabled();
        _bitLockerEnabled = SecurityService.GetBitLockerStatus();
        _ = LoadProcessesAsync();
    }

    [RelayCommand]
    private async Task LoadProcessesAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading processes...";

        await Task.Run(() =>
        {
            var processes = SecurityService.GetRunningProcesses();
            SafeDispatch(() => RunningProcesses = new ObservableCollection<ProcessInfo>(processes));
        });

        StatusMessage = $"Loaded {RunningProcesses.Count} processes.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadStartupItemsAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading startup items...";

        var items = await SecurityService.GetStartupItemsAsync();
        StartupItems = new ObservableCollection<StartupItem>(items);

        StatusMessage = $"Found {StartupItems.Count} startup items.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadScheduledTasksAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading scheduled tasks...";

        await Task.Run(() =>
        {
            var tasks = SecurityService.GetScheduledTasks();
            SafeDispatch(() => ScheduledTasks = new ObservableCollection<ScheduledTaskInfo>(tasks));
        });

        StatusMessage = $"Found {ScheduledTasks.Count} scheduled tasks.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task CheckUnsignedBinariesAsync()
    {
        IsBusy = true;
        StatusMessage = "Checking for unsigned binaries...";

        await Task.Run(() =>
        {
            var unsigned = SecurityService.GetUnsignedBinaries();
            SafeDispatch(() => UnsignedBinaries = new ObservableCollection<string>(unsigned));
        });

        StatusMessage = $"Found {UnsignedBinaries.Count} unsigned binaries.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task KillSelectedProcessAsync()
    {
        if (string.IsNullOrEmpty(SelectedProcessName)) return;

        IsBusy = true;
        StatusMessage = $"Terminating {SelectedProcessName}...";

        var process = RunningProcesses.FirstOrDefault(p => p.Name == SelectedProcessName);
        if (process != null)
        {
            var result = await SecurityService.KillProcessAsync(process.Id);
            StatusMessage = result;
            await LoadProcessesAsync();
        }

        IsBusy = false;
    }

    [RelayCommand]
    private void ToggleUsbStorage()
    {
        SecurityService.SetUsbStorageEnabled(!UsbStorageEnabled);
        UsbStorageEnabled = SecurityService.GetUsbStorageEnabled();
        StatusMessage = UsbStorageEnabled ? "USB storage enabled." : "USB storage disabled.";
    }

    [RelayCommand]
    private async Task RefreshAllAsync()
    {
        await LoadProcessesAsync();
    }
}
