using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;
using Serilog;

namespace BhavaniSupportSuite.ViewModels;

public partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly DiagnosticsService _diagnostics;
    private CancellationTokenSource? _telemetryCts;

    [ObservableProperty]
    private ObservableCollection<string> _systemInfo = new();

    [ObservableProperty]
    private ObservableCollection<string> _repairLog = new();

    [ObservableProperty]
    private bool _isRepairRunning;

    [ObservableProperty]
    private string _repairStatus = string.Empty;

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
    private float _cpuTemperatureF;

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
        StartTelemetryAsync();
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
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get domain name");
            Domain = Environment.UserDomainName;
        }
    }

    private void StartTelemetryAsync()
    {
        _telemetryCts = new CancellationTokenSource();
        _ = RunTelemetryAsync(_telemetryCts.Token);
    }

    private async Task RunTelemetryAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    CpuUsage = DiagnosticsService.GetCpuUsage();
                    MemoryUsage = DiagnosticsService.GetMemoryUsage();
                    DiskIO = DiagnosticsService.GetDiskReadBytes() / (1024 * 1024);
                    NetworkThroughput = DiagnosticsService.GetNetworkBytes() / (1024 * 1024);
                    CpuTemperature = DiagnosticsService.GetCpuTemperature();
                    CpuTemperatureF = CpuTemperature * 9f / 5f + 32f;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to update telemetry");
                }
                await Task.Delay(2000, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on disposal
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Telemetry loop failed");
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

        // Subscribe to output events
        var outputHandler = new Action<string>(msg => SafeDispatch(() => RepairLog.Add($"  {msg}")));
        var errorHandler = new Action<string>(msg => SafeDispatch(() => RepairLog.Add($"  [ERR] {msg}")));

        _diagnostics.OutputReceived += outputHandler;
        _diagnostics.ErrorReceived += errorHandler;

        try
        {
            foreach (var (name, fileName, args) in steps)
            {
                RepairLog.Add($"[{DateTime.Now:HH:mm:ss}] Starting: {name}...");
                RepairStatus = $"Running: {name}";

                var exitCode = await _diagnostics.RunCommandAsync(fileName, args);
                RepairLog.Add($"[{DateTime.Now:HH:mm:ss}] {name} completed with exit code: {exitCode}");
            }

            RepairLog.Add($"[{DateTime.Now:HH:mm:ss}] All repairs completed.");
            RepairStatus = "Repairs completed.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Repair failed");
            RepairLog.Add($"[{DateTime.Now:HH:mm:ss}] Repair failed: {ex.Message}");
            RepairStatus = "Repair failed.";
        }
        finally
        {
            // Unsubscribe from events
            _diagnostics.OutputReceived -= msg => SafeDispatch(() => RepairLog.Add($"  {msg}"));
            _diagnostics.ErrorReceived -= msg => SafeDispatch(() => RepairLog.Add($"  [ERR] {msg}"));
            IsRepairRunning = false;
            RepairStatus = "Repairs completed.";
        }
    }

    public override void Dispose()
    {
        _telemetryCts?.Cancel();
        _telemetryCts?.Dispose();
    }
}