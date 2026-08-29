using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;

namespace BhavaniSupportSuite.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly DiagnosticsService _diagnostics;

    [ObservableProperty]
    private ObservableCollection<string> _systemInfo = new();

    [ObservableProperty]
    private ObservableCollection<string> _repairLog = new();

    [ObservableProperty]
    private bool _isRepairRunning;

    [ObservableProperty]
    private string _repairStatus;

    [ObservableProperty]
    private float _cpuUsage;

    [ObservableProperty]
    private float _memoryUsage;

    [ObservableProperty]
    private float _diskIO;

    [ObservableProperty]
    private float _networkThroughput;

    [ObservableProperty]
    private float _cpuTemperature;

    [ObservableProperty]
    private string _hostname = string.Empty;

    [ObservableProperty]
    private string _osBuild = string.Empty;

    [ObservableProperty]
    private string _installedRam = string.Empty;

    [ObservableProperty]
    private string _processor = string.Empty;

    [ObservableProperty]
    private string _uptime = string.Empty;

    [ObservableProperty]
    private string _domain = string.Empty;

    [ObservableProperty]
    private string _gpuInfo = string.Empty;

    public DashboardViewModel(DiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
        Title = "Unified Dashboard";
        _repairStatus = "Ready";
        _ = LoadSystemInfoAsync();
        _ = StartTelemetryAsync();
    }

    private async Task LoadSystemInfoAsync()
    {
        var info = await DiagnosticsService.GetSystemInfoAsync();
        SystemInfo = new ObservableCollection<string>(info);

        foreach (var line in info)
        {
            if (line.StartsWith("Hostname:")) Hostname = line.Replace("Hostname:", "").Trim();
            if (line.StartsWith("OS:")) OsBuild = line.Replace("OS:", "").Trim();
            if (line.StartsWith("Installed RAM:")) InstalledRam = line.Replace("Installed RAM:", "").Trim();
            if (line.StartsWith("Processor:")) Processor = line.Replace("Processor:", "").Trim();
            if (line.StartsWith("Uptime:")) Uptime = line.Replace("Uptime:", "").Trim();
            if (line.StartsWith("GPU:")) GpuInfo = line.Replace("GPU:", "").Trim();
        }

        try { Domain = System.DirectoryServices.ActiveDirectory.Domain.GetComputerDomain().Name; }
        catch { Domain = Environment.UserDomainName; }
    }

    private async Task StartTelemetryAsync()
    {
        while (true)
        {
            try
            {
                CpuUsage = DiagnosticsService.GetCpuUsage();
                MemoryUsage = DiagnosticsService.GetMemoryUsage();
                DiskIO = DiagnosticsService.GetDiskReadBytes() / (1024 * 1024);
                NetworkThroughput = DiagnosticsService.GetNetworkBytes() / (1024 * 1024);
                CpuTemperature = DiagnosticsService.GetCpuTemperature();
            }
            catch { }
            await Task.Delay(2000);
        }
    }

    [RelayCommand]
    private async Task RunRepairAsync()
    {
        if (IsRepairRunning) return;

        IsRepairRunning = true;
        RepairStatus = "Running repairs...";
        RepairLog.Clear();

        var steps = new (string Name, string File, string Args)[]
        {
            ("System File Checker", "sfc", "/scannow"),
            ("DISM Health Restore", "DISM.exe", "/Online /Cleanup-Image /RestoreHealth"),
            ("WinSock Reset", "netsh", "winsock reset"),
            ("DNS Flush", "ipconfig", "/flushdns")
        };

        foreach (var (name, fileName, args) in steps)
        {
            RepairLog.Add($"[{DateTime.Now:HH:mm:ss}] Starting: {name}...");
            RepairStatus = $"Running: {name}";

            _diagnostics.OutputReceived += msg => App.Current.Dispatcher.Invoke(() => RepairLog.Add($"  {msg}"));
            _diagnostics.ErrorReceived += msg => App.Current.Dispatcher.Invoke(() => RepairLog.Add($"  [ERR] {msg}"));

            var exitCode = await _diagnostics.RunCommandAsync(fileName, args);
            RepairLog.Add($"[{DateTime.Now:HH:mm:ss}] {name} completed with exit code: {exitCode}");
        }

        RepairLog.Add($"[{DateTime.Now:HH:mm:ss}] All repairs completed.");
        RepairStatus = "Repairs completed.";
        IsRepairRunning = false;
    }
}
