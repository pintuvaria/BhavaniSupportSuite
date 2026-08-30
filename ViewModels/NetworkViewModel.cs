using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;

namespace BhavaniSupportSuite.ViewModels;

public partial class NetworkViewModel : ViewModelBase
{
    private readonly NetworkScanner _scanner;
    private readonly DiagnosticsService _diagnostics;
    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private ObservableCollection<ScanResult> _scanResults = new();

    [ObservableProperty]
    private ObservableCollection<PortScanResult> _portResults = new();

    [ObservableProperty]
    private ObservableCollection<string> _networkInfo = new();

    [ObservableProperty]
    private ObservableCollection<IpConflictResult> _ipConflicts = new();

    [ObservableProperty]
    private string _targetSubnet = string.Empty;

    [ObservableProperty]
    private string _targetIp = string.Empty;

    [ObservableProperty]
    private string _portRange = "22,80,135,445,3389,8080";

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanStatus = string.Empty;

    [ObservableProperty]
    private int _scanProgress;

    [ObservableProperty]
    private int _scanTotal;

    [ObservableProperty]
    private ScanResult? _selectedHost;

    public NetworkViewModel(NetworkScanner scanner, DiagnosticsService diagnostics)
    {
        _scanner = scanner;
        _diagnostics = diagnostics;
        Title = "Network Scanner";
        _scanStatus = "Ready";
    }

    [RelayCommand]
    private async Task StartSubnetScanAsync()
    {
        if (IsScanning || string.IsNullOrEmpty(TargetSubnet)) return;

        IsScanning = true;
        ScanResults.Clear();
        ScanStatus = "Scanning...";

        try
        {
            _scanCts = new CancellationTokenSource();
            var results = await _scanner.ScanSubnetAsync(TargetSubnet, _scanCts.Token);
            SafeDispatch(() =>
            {
                foreach (var r in results)
                    ScanResults.Add(r);
            });
            ScanStatus = $"Scan complete. Found {results.Count} hosts.";
        }
        catch (OperationCanceledException) { ScanStatus = "Scan cancelled."; }
        catch (Exception ex) { ScanStatus = $"Error: {ex.Message}"; }

        IsScanning = false;
    }

    [RelayCommand]
    private void StopScan()
    {
        _scanCts?.Cancel();
        IsScanning = false;
        ScanStatus = "Scan stopped.";
    }

    [RelayCommand]
    private async Task ScanPortsAsync()
    {
        if (string.IsNullOrEmpty(TargetIp)) return;

        IsScanning = true;
        PortResults.Clear();
        ScanStatus = $"Scanning ports on {TargetIp}...";

        try
        {
            var ports = PortRange.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => int.TryParse(p, out var port) ? port : 0).Where(p => p > 0).ToArray();

            var results = await _scanner.ScanPortsAsync(TargetIp, ports);
            var collection = new ObservableCollection<PortScanResult>(results);
            SafeDispatch(() => PortResults = collection);

            var openPorts = results.Count(r => r.IsOpen);
            ScanStatus = $"Port scan complete. {openPorts}/{ports.Length} ports open.";
        }
        catch (Exception ex) { ScanStatus = $"Error: {ex.Message}"; }

        IsScanning = false;
    }

    [RelayCommand]
    private async Task DetectIpConflictsAsync()
    {
        if (string.IsNullOrEmpty(TargetSubnet)) return;

        IsScanning = true;
        IpConflicts.Clear();
        ScanStatus = "Detecting IP conflicts...";

        try
        {
            var conflicts = await _scanner.DetectIpConflictsAsync(TargetSubnet);
            var collection = new ObservableCollection<IpConflictResult>(conflicts);
            SafeDispatch(() => IpConflicts = collection);
            ScanStatus = $"Conflict detection complete. {conflicts.Count} conflicts found.";
        }
        catch (Exception ex) { ScanStatus = $"Error: {ex.Message}"; }

        IsScanning = false;
    }

    [RelayCommand]
    private async Task LoadNetworkInfoAsync()
    {
        IsScanning = true;
        ScanStatus = "Loading network information...";

        try
        {
            var info = await NetworkScanner.GetNetworkInfoAsync();
            var lines = new ObservableCollection<string>(info);
            SafeDispatch(() => NetworkInfo = lines);
            ScanStatus = "Network information loaded.";
        }
        catch (Exception ex) { ScanStatus = $"Error: {ex.Message}"; }

        IsScanning = false;
    }

    [RelayCommand]
    private void ClearResults()
    {
        ScanResults.Clear();
        PortResults.Clear();
        NetworkInfo.Clear();
        IpConflicts.Clear();
        ScanStatus = "Cleared.";
    }
}
