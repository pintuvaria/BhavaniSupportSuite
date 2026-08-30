using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;

namespace BhavaniSupportSuite.ViewModels;

public partial class HardwareViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<HardwareInfo> _hardwareInfo = new();

    [ObservableProperty]
    private ObservableCollection<SmartStatus> _smartStatuses = new();

    [ObservableProperty]
    private ObservableCollection<string> _batteryInfo = new();

    [ObservableProperty]
    private ObservableCollection<string> _memoryDiagnosticResults = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private float _cpuTemperature;

    [ObservableProperty]
    private float _cpuTemperatureF;

    [ObservableProperty]
    private string _cpuTempStatus = "Normal";

    public HardwareViewModel()
    {
        Title = "Hardware Diagnostics";
        _statusMessage = "Ready";
        _ = LoadHardwareInfoAsync();
        _ = StartThermalMonitorAsync();
    }

    private async Task LoadHardwareInfoAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading hardware information...";

        await Task.Run(() =>
        {
            var info = HardwareService.GetDetailedHardwareInfo();
            SafeDispatch(() => HardwareInfo = new ObservableCollection<HardwareInfo>(info));
        });

        StatusMessage = $"Loaded {HardwareInfo.Count} hardware entries.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task CheckSmartStatusAsync()
    {
        IsBusy = true;
        StatusMessage = "Checking SMART status...";

        await Task.Run(() =>
        {
            var statuses = HardwareService.GetSmartStatus();
            SafeDispatch(() => SmartStatuses = new ObservableCollection<SmartStatus>(statuses));
        });

        StatusMessage = $"SMART check complete. {SmartStatuses.Count} drives found.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task CheckBatteryAsync()
    {
        IsBusy = true;
        StatusMessage = "Checking battery...";

        var info = await Task.Run(() => HardwareService.GetBatteryDetails());
        BatteryInfo = new ObservableCollection<string>(info);

        StatusMessage = "Battery check complete.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task RunMemoryDiagnosticAsync()
    {
        IsBusy = true;
        StatusMessage = "Running memory diagnostic...";

        var results = await HardwareService.RunMemoryDiagnosticAsync();
        MemoryDiagnosticResults = new ObservableCollection<string>(results);

        StatusMessage = "Memory diagnostic complete.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task RefreshHardwareAsync()
    {
        await LoadHardwareInfoAsync();
    }

    private async Task StartThermalMonitorAsync()
    {
        while (true)
        {
            try
            {
                CpuTemperature = HardwareService.GetCpuTemperature();
                CpuTemperatureF = CpuTemperature * 9f / 5f + 32f;
                CpuTempStatus = CpuTemperature switch
                {
                    < 50 => "Normal",
                    < 70 => "Warm",
                    < 85 => "Hot",
                    _ => "Critical"
                };
            }
            catch { }
            await Task.Delay(3000);
        }
    }
}
