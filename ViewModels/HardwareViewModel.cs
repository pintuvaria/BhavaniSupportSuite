using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;
using Serilog;

namespace BhavaniSupportSuite.ViewModels;

public partial class HardwareViewModel : ViewModelBase, IDisposable
{
    private CancellationTokenSource? _thermalCts;

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
        StartThermalMonitorAsync();
    }

    private async Task LoadHardwareInfoAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading hardware information...";

        try
        {
            await Task.Run(() =>
            {
                var info = HardwareService.GetDetailedHardwareInfo();
                SafeDispatch(() => HardwareInfo = new ObservableCollection<HardwareInfo>(info));
            });

            StatusMessage = $"Loaded {HardwareInfo.Count} hardware entries.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load hardware info");
            StatusMessage = "Failed to load hardware information.";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task CheckSmartStatusAsync()
    {
        IsBusy = true;
        StatusMessage = "Checking SMART status...";

        try
        {
            await Task.Run(() =>
            {
                var statuses = HardwareService.GetSmartStatus();
                SafeDispatch(() => SmartStatuses = new ObservableCollection<SmartStatus>(statuses));
            });

            StatusMessage = $"SMART check complete. {SmartStatuses.Count} drives found.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SMART status check failed");
            StatusMessage = $"SMART check failed: {ex.Message}";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task CheckBatteryAsync()
    {
        IsBusy = true;
        StatusMessage = "Checking battery...";

        try
        {
            var info = await Task.Run(() => HardwareService.GetBatteryDetails());
            BatteryInfo = new ObservableCollection<string>(info);

            StatusMessage = "Battery check complete.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Battery check failed");
            StatusMessage = $"Battery check failed: {ex.Message}";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task RunMemoryDiagnosticAsync()
    {
        IsBusy = true;
        StatusMessage = "Running memory diagnostic...";

        try
        {
            var results = await HardwareService.RunMemoryDiagnosticAsync();
            MemoryDiagnosticResults = new ObservableCollection<string>(results);

            StatusMessage = "Memory diagnostic complete.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Memory diagnostic failed");
            StatusMessage = $"Memory diagnostic failed: {ex.Message}";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task RefreshHardwareAsync()
    {
        await LoadHardwareInfoAsync();
    }

    private void StartThermalMonitorAsync()
    {
        _thermalCts = new CancellationTokenSource();
        _ = RunThermalMonitorAsync(_thermalCts.Token);
    }

    private async Task RunThermalMonitorAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
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
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to get CPU temperature");
                }
                await Task.Delay(3000, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on disposal
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Thermal monitor loop failed");
        }
    }

    public override void Dispose()
    {
        _thermalCts?.Cancel();
        _thermalCts?.Dispose();
    }
}