using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;

namespace BhavaniSupportSuite.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly DiagnosticsService _diagnostics;

    [ObservableProperty]
    private ObservableCollection<string> _reportLog = new();

    [ObservableProperty]
    private ObservableCollection<RemoteConnection> _savedConnections = new()
    {
        new RemoteConnection { Name = "Localhost RDP", Host = "127.0.0.1", Port = 3389, Protocol = "RDP" },
        new RemoteConnection { Name = "Gateway SSH", Host = "192.168.1.1", Port = 22, Protocol = "SSH" },
    };

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage;

    [ObservableProperty]
    private string _connectionHost = string.Empty;

    [ObservableProperty]
    private int _connectionPort = 3389;

    [ObservableProperty]
    private string _connectionUsername = string.Empty;

    [ObservableProperty]
    private string _selectedProtocol = "RDP";

    [ObservableProperty]
    private string _reportPath = string.Empty;

    public ReportsViewModel()
    {
        _diagnostics = new DiagnosticsService();
        Title = "Reports & Remote";
        StatusMessage = "Ready";
        ReportPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    [RelayCommand]
    private async Task LaunchRdpAsync()
    {
        if (string.IsNullOrEmpty(ConnectionHost)) { StatusMessage = "Enter a hostname/IP."; return; }
        StatusMessage = $"Launching RDP to {ConnectionHost}...";
        await ReportsService.LaunchRdpAsync(ConnectionHost, ConnectionPort, ConnectionUsername);
        StatusMessage = "RDP session launched.";
    }

    [RelayCommand]
    private async Task LaunchSshAsync()
    {
        if (string.IsNullOrEmpty(ConnectionHost)) { StatusMessage = "Enter a hostname/IP."; return; }
        StatusMessage = $"Launching SSH to {ConnectionHost}...";
        await ReportsService.LaunchSshAsync(ConnectionHost, ConnectionPort, ConnectionUsername);
        StatusMessage = "SSH session launched.";
    }

    [RelayCommand]
    private async Task LaunchVncAsync()
    {
        if (string.IsNullOrEmpty(ConnectionHost)) { StatusMessage = "Enter a hostname/IP."; return; }
        StatusMessage = $"Launching VNC to {ConnectionHost}...";
        await ReportsService.LaunchVncAsync(ConnectionHost, ConnectionPort);
        StatusMessage = "VNC session launched.";
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        IsBusy = true;
        StatusMessage = "Generating diagnostic report...";
        ReportLog.Clear();

        try
        {
            var hostname = Environment.MachineName;
            var systemData = new Dictionary<string, string>
            {
                ["Hostname"] = hostname,
                ["Username"] = Environment.UserName,
                ["OS"] = Environment.OSVersion.ToString(),
                ["Architecture"] = Environment.Is64BitOperatingSystem ? "x64" : "x86",
                ["Processors"] = Environment.ProcessorCount.ToString(),
                ["Uptime"] = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"d\.hh\:mm\:ss"),
                ["CPU Usage"] = $"{DiagnosticsService.GetCpuUsage():F1}%",
                ["Memory Usage"] = $"{DiagnosticsService.GetMemoryUsage():F1}%",
                ["Generated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["Prepared By"] = "Dharmesh Varia, Lead Engineer"
            };

            var html = await ReportsService.GenerateHtmlReportAsync(hostname, systemData);
            var filePath = Path.Combine(ReportPath, $"BhavaniReport_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            await ReportsService.SaveReportAsync(html, filePath);

            ReportLog.Add($"Report generated: {filePath}");
            ReportsService.OpenReport(filePath);

            StatusMessage = $"Report saved to: {filePath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task GenerateServiceReceiptAsync()
    {
        IsBusy = true;
        StatusMessage = "Generating service receipt...";

        var items = new List<(string Item, string Status, string Notes)>
        {
            ("System Diagnostic", "Completed", "Full system scan performed"),
            ("Security Audit", "Completed", "Processes and startup items checked"),
            ("Network Scan", "Completed", "Subnet scan and port audit"),
            ("Storage Clean", "Completed", "Deep clean and driver backup"),
        };

        var html = await ReportsService.GenerateServiceReceiptAsync(Environment.MachineName, items);
        var filePath = Path.Combine(ReportPath, $"ServiceReceipt_{DateTime.Now:yyyyMMdd_HHmmss}.html");
        await ReportsService.SaveReportAsync(html, filePath);
        ReportsService.OpenReport(filePath);

        StatusMessage = $"Receipt saved to: {filePath}";
        IsBusy = false;
    }

    [RelayCommand]
    private void SaveConnection()
    {
        if (string.IsNullOrEmpty(ConnectionHost)) return;

        SavedConnections.Add(new RemoteConnection
        {
            Name = $"{SelectedProtocol} - {ConnectionHost}",
            Host = ConnectionHost,
            Port = ConnectionPort,
            Protocol = SelectedProtocol,
            Username = ConnectionUsername,
            LastUsed = DateTime.Now
        });

        StatusMessage = $"Connection saved: {ConnectionHost}";
    }
}
