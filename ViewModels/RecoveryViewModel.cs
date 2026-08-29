using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;

namespace BhavaniSupportSuite.ViewModels;

public partial class RecoveryViewModel : ViewModelBase
{
    private readonly DiagnosticsService _diagnostics;

    [ObservableProperty]
    private ObservableCollection<string> _auditLog = new();

    [ObservableProperty]
    private ObservableCollection<UserAuditInfo> _localUsers = new();

    [ObservableProperty]
    private ObservableCollection<ServiceInfo> _services = new();

    [ObservableProperty]
    private ObservableCollection<EventLogEntry> _criticalEvents = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage;

    [ObservableProperty]
    private string? _selectedServiceName;

    public RecoveryViewModel(DiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
        Title = "System Recovery";
        _statusMessage = "Ready";
        _ = LoadServicesAsync();
    }

    [RelayCommand]
    private async Task AuditLocalUsersAsync()
    {
        IsBusy = true;
        StatusMessage = "Auditing local user accounts...";
        LocalUsers.Clear();
        AuditLog.Clear();

        var output = await _diagnostics.RunCommandCapturedAsync("net", "user");
        AuditLog.Add(output);

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines.Skip(4).SkipLast(2))
        {
            var username = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrEmpty(username) && !username.Contains("---"))
            {
                LocalUsers.Add(new UserAuditInfo
                {
                    Username = username,
                    Status = "Active",
                    LastLogon = "N/A"
                });
            }
        }

        StatusMessage = $"Found {LocalUsers.Count} local user accounts.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadServicesAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading services...";
        Services.Clear();

        try
        {
            await Task.Run(() =>
            {
                var serviceController = System.ServiceProcess.ServiceController.GetServices();
                foreach (var svc in serviceController)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Services.Add(new ServiceInfo
                        {
                            Name = svc.ServiceName,
                            DisplayName = svc.DisplayName,
                            Status = svc.Status.ToString(),
                            StartType = svc.StartType.ToString()
                        });
                    });
                }
            });
            StatusMessage = $"Loaded {Services.Count} services.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading services: {ex.Message}";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task StartServiceAsync(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName)) return;

        StatusMessage = $"Starting service: {serviceName}...";
        var exitCode = await _diagnostics.RunCommandCapturedAsync("sc", $"start \"{serviceName}\"");
        AuditLog.Add($"[{DateTime.Now:HH:mm:ss}] Start {serviceName}: {exitCode}");
        await LoadServicesAsync();
    }

    [RelayCommand]
    private async Task StopServiceAsync(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName)) return;

        StatusMessage = $"Stopping service: {serviceName}...";
        var exitCode = await _diagnostics.RunCommandCapturedAsync("sc", $"stop \"{serviceName}\"");
        AuditLog.Add($"[{DateTime.Now:HH:mm:ss}] Stop {serviceName}: {exitCode}");
        await LoadServicesAsync();
    }

    [RelayCommand]
    private async Task AnalyzeEventLogsAsync()
    {
        IsBusy = true;
        StatusMessage = "Analyzing event logs (last 24h)...";
        CriticalEvents.Clear();

        try
        {
            await Task.Run(() =>
            {
                var logNames = new[] { "System", "Application", "Security" };
                foreach (var logName in logNames)
                {
                    var log = new System.Diagnostics.EventLog(logName);
                    var cutoff = DateTime.Now.AddHours(-24);

                    foreach (System.Diagnostics.EventLogEntry entry in log.Entries)
                    {
                        if (entry.TimeGenerated >= cutoff &&
                            (entry.EntryType == System.Diagnostics.EventLogEntryType.Error ||
                             entry.EntryType == System.Diagnostics.EventLogEntryType.Warning))
                        {
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                CriticalEvents.Add(new EventLogEntry
                                {
                                    Source = entry.Source,
                                    Type = entry.EntryType.ToString(),
                                    Message = entry.Message.Length > 200 ? entry.Message[..200] + "..." : entry.Message,
                                    TimeGenerated = entry.TimeGenerated.ToString("yyyy-MM-dd HH:mm:ss"),
                                    LogName = logName
                                });
                            });
                        }
                    }
                }
            });

            StatusMessage = $"Found {CriticalEvents.Count} critical/warning events in last 24h.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error analyzing event logs: {ex.Message}";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task CheckDriverStatusAsync()
    {
        IsBusy = true;
        StatusMessage = "Checking driver status...";
        AuditLog.Clear();

        var output = await _diagnostics.RunCommandCapturedAsync("pnputil", "/enum-drivers");
        AuditLog.Add(output);

        StatusMessage = "Driver status check complete.";
        IsBusy = false;
    }
}

public class UserAuditInfo
{
    public string Username { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LastLogon { get; set; } = string.Empty;
}

public class ServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StartType { get; set; } = string.Empty;
}

public class EventLogEntry
{
    public string Source { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TimeGenerated { get; set; } = string.Empty;
    public string LogName { get; set; } = string.Empty;
}
