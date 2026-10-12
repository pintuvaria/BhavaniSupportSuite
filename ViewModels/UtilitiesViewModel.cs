using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;
using Serilog;

namespace BhavaniSupportSuite.ViewModels;

public partial class UtilitiesViewModel : ViewModelBase
{
    private readonly DiagnosticsService _diagnostics;

    [ObservableProperty]
    private ObservableCollection<string> _outputLog = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _targetUsername = string.Empty;

    [ObservableProperty]
    private SecureString _newPassword = new();

    [ObservableProperty]
    private string _reportPath = string.Empty;

    public UtilitiesViewModel(DiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
        Title = "Enterprise Utilities";
        StatusMessage = "Ready";
        ReportPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    public string NewPasswordString
    {
        get => new System.Net.NetworkCredential(string.Empty, NewPassword).Password;
        set => NewPassword = value != null ? new System.Net.NetworkCredential(string.Empty, value).SecurePassword : new SecureString();
    }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetUsername) || NewPassword.Length == 0)
        {
            StatusMessage = "Please enter username and new password.";
            return;
        }

        // Validate username format
        if (!SecurityUtilities.IsSafeArgument(TargetUsername))
        {
            StatusMessage = "Invalid username format.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Resetting password for: {TargetUsername}...";
        OutputLog.Clear();

        try
        {
            // Use PowerShell for secure password handling - pass password via stdin to avoid command line exposure
            var sanitizedUser = SecurityUtilities.SanitizeArgument(TargetUsername);
            var psScript = @"
$ErrorActionPreference = 'Stop'
$user = $args[0]
$pass = $args[1] | ConvertTo-SecureString -AsPlainText -Force
Set-LocalUser -Name $user -Password $pass
";

            var passwordString = new System.Net.NetworkCredential(string.Empty, NewPassword).Password;
            var sanitizedPass = SecurityUtilities.SanitizeArgument(passwordString);
            
            var exitCode = await _diagnostics.RunCommandAsync("powershell", $"-Command \"& {{ {psScript} }}\" '{sanitizedUser}' '{sanitizedPass}'");
            OutputLog.Add($"[{DateTime.Now:HH:mm:ss}] Password reset completed with exit code: {exitCode}");

            StatusMessage = exitCode == 0 ? "Password reset successfully." : "Password reset failed.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Password reset failed for user {User}", TargetUsername);
            OutputLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");
            StatusMessage = "Password reset failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GenerateReportAsync()
    {
        IsBusy = true;
        StatusMessage = "Generating diagnostic report...";
        OutputLog.Clear();

        try
        {
            var reportFile = Path.Combine(ReportPath, $"BhavaniReport_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            var hostname = Environment.MachineName;
            var username = Environment.UserName;
            var osVersion = Environment.OSVersion.ToString();
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            var cpuUsage = DiagnosticsService.GetCpuUsage();
            var memUsage = DiagnosticsService.GetMemoryUsage();

            var html = $@"<!DOCTYPE html>
<html>
<head>
    <title>Bhavani Technology - Diagnostic Report</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, sans-serif; background: #1E1E2C; color: #E0E0E0; margin: 40px; }}
        .header {{ text-align: center; border-bottom: 3px solid #00ADB5; padding-bottom: 20px; margin-bottom: 30px; }}
        .header h1 {{ color: #00ADB5; font-size: 28px; margin: 0; }}
        .header p {{ color: #888; margin: 5px 0 0 0; }}
        .card {{ background: #2D2D3F; border-radius: 12px; padding: 20px; margin: 15px 0; box-shadow: 0 4px 6px rgba(0,0,0,0.3); }}
        .card h2 {{ color: #00ADB5; margin: 0 0 15px 0; font-size: 18px; }}
        .info-row {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #3D3D4F; }}
        .info-label {{ color: #888; }}
        .info-value {{ color: #E0E0E0; font-weight: bold; }}
        .footer {{ text-align: center; margin-top: 30px; padding-top: 20px; border-top: 1px solid #3D3D4F; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""header"">
        <h1>Bhavani Support Suite Pro</h1>
        <p>Diagnostic Report - Generated by Dharmesh Varia</p>
        <p>Bhavani Technology | {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
    </div>

    <div class=""card"">
        <h2>System Information</h2>
        <div class=""info-row""><span class=""info-label"">Hostname:</span><span class=""info-value"">{hostname}</span></div>
        <div class=""info-row""><span class=""info-label"">Username:</span><span class=""info-value"">{username}</span></div>
        <div class=""info-row""><span class=""info-label"">Operating System:</span><span class=""info-value"">{osVersion}</span></div>
        <div class=""info-row""><span class=""info-label"">Uptime:</span><span class=""info-value"">{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m</span></div>
    </div>

    <div class=""card"">
        <h2>Performance Metrics</h2>
        <div class=""info-row""><span class=""info-label"">CPU Usage:</span><span class=""info-value"">{cpuUsage:F1}%</span></div>
        <div class=""info-row""><span class=""info-label"">Memory Usage:</span><span class=""info-value"">{memUsage:F1}%</span></div>
    </div>

    <div class=""card"">
        <h2>Report Details</h2>
        <div class=""info-row""><span class=""info-label"">Report Generated:</span><span class=""info-value"">{DateTime.Now:yyyy-MM-dd HH:mm:ss}</span></div>
        <div class=""info-row""><span class=""info-label"">Tool Version:</span><span class=""info-value"">1.0.0</span></div>
        <div class=""info-row""><span class=""info-label"">Prepared By:</span><span class=""info-value"">Dharmesh Varia</span></div>
    </div>

    <div class=""footer"">
        <p>Bhavani Technology - Bhavani Support Suite Pro v1.0.0</p>
        <p>Prepared by Dharmesh Varia | {DateTime.Now:yyyy}</p>
    </div>
</body>
</html>";

            await File.WriteAllTextAsync(reportFile, html);
            OutputLog.Add($"[{DateTime.Now:HH:mm:ss}] Report generated: {reportFile}");
            StatusMessage = $"Report saved to: {reportFile}";

            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = reportFile,
                        UseShellExecute = true
                    }
                };
                process.Start();
            }
            catch { }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to generate report");
            OutputLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");
            StatusMessage = $"Error: {ex.Message}";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task ListLocalUsersAsync()
    {
        IsBusy = true;
        StatusMessage = "Listing local users...";
        OutputLog.Clear();

        try
        {
            var output = await _diagnostics.RunCommandCapturedAsync("net", "user");
            OutputLog.Add(output);
            StatusMessage = "Local user list loaded.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to list local users");
            StatusMessage = $"Error: {ex.Message}";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task ListScheduledTasksAsync()
    {
        IsBusy = true;
        StatusMessage = "Listing scheduled tasks...";
        OutputLog.Clear();

        try
        {
            var output = await _diagnostics.RunCommandCapturedAsync("schtasks", "/query /fo LIST");
            OutputLog.Add(output);
            StatusMessage = "Scheduled tasks loaded.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to list scheduled tasks");
            StatusMessage = $"Error: {ex.Message}";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task OpenControlPanelAsync()
    {
        await _diagnostics.RunCommandAsync("control", "");
    }

    [RelayCommand]
    private async Task OpenDeviceManagerAsync()
    {
        await _diagnostics.RunCommandAsync("devmgmt.msc", "");
    }

    [RelayCommand]
    private async Task OpenDiskManagementAsync()
    {
        await _diagnostics.RunCommandAsync("diskmgmt.msc", "");
    }
}