using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading.Tasks;
using BhavaniSupportSuite.Core;
using Serilog;

namespace BhavaniSupportSuite.Services;

public class WindowsUpdateInfo
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string KBArticle { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public bool IsDownloaded { get; set; }
    public bool IsInstalled { get; set; }
    public string LastChange { get; set; } = string.Empty;
}

public class PatchService
{
    public async Task<List<WindowsUpdateInfo>> GetWindowsUpdateHistoryAsync()
    {
        var updates = new List<WindowsUpdateInfo>();
        try
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo();
            process.StartInfo.FileName = "powershell";
            process.StartInfo.Arguments = "-Command \"Get-WindowsUpdateLog | Select-Object -First 50 | Format-Table -AutoSize\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get Windows Update history");
        }
        return updates;
    }

    public async Task<List<WindowsUpdateInfo>> GetPendingUpdatesAsync()
    {
        var updates = new List<WindowsUpdateInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\Microsoft\\Windows\\WindowsUpdate",
                "SELECT * FROM Microsoft_Update WHERE IsInstalled = 0");
            
            foreach (var obj in searcher.Get())
            {
                updates.Add(new WindowsUpdateInfo
                {
                    Title = obj["Title"]?.ToString() ?? string.Empty,
                    Description = obj["Description"]?.ToString() ?? string.Empty,
                    KBArticle = obj["KBArticleID"]?.ToString() ?? string.Empty,
                    Classification = obj["Categories"]?.ToString() ?? string.Empty,
                    IsDownloaded = Convert.ToBoolean(obj["IsDownloaded"] ?? false),
                    IsInstalled = Convert.ToBoolean(obj["IsInstalled"] ?? false),
                    LastChange = obj["LastDeploymentChangeTime"]?.ToString() ?? string.Empty
                });
            }
        }
        catch
        {
            try
            {
                var process = new Process();
                process.StartInfo = new ProcessStartInfo();
                process.StartInfo.FileName = "powershell";
                process.StartInfo.Arguments = "-Command \"$session = New-Object -ComObject Microsoft.Update.Session; $searcher = $session.CreateUpdateSearcher(); $result = $searcher.Search('IsInstalled=0'); $result.Updates | Select-Object Title, Description, KBArticleIDs, Categories, IsDownloaded, IsInstalled | ConvertTo-Json\"";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get pending updates");
            }
        }
        return updates;
    }

    public async Task<List<string>> GetUpdateStatusAsync()
    {
        var status = new List<string>();
        try
        {
            var psi = new ProcessStartInfo();
            psi.FileName = "powershell";
            psi.Arguments = "-Command \"$session = New-Object -ComObject Microsoft.Update.Session; $searcher = $session.CreateUpdateSearcher(); $result = $searcher.Search('IsInstalled=0'); Write-Host 'Pending updates: $($result.Updates.Count)'; $history = $searcher.QueryHistory(0, $searcher.GetTotalHistoryCount()); $installed = $history | Where-Object {$_.ResultCode -eq 2}; Write-Host 'Installed updates: $($installed.Count)'; $failed = $history | Where-Object {$_.ResultCode -ne 2}; Write-Host 'Failed updates: $($failed.Count)'\"";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.CreateNoWindow = true;
            var process = new Process { StartInfo = psi };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();

            foreach (var line in output.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    status.Add(line.Trim());
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get update status");
            status.Add("Unable to retrieve update status.");
        }
        return status;
    }

    public async Task<int> InstallUpdatesAsync(System.IProgress<string>? progress = null)
    {
        try
        {
            var psi = new ProcessStartInfo();
            psi.FileName = "powershell";
            psi.Arguments = "-Command \"$session = New-Object -ComObject Microsoft.Update.Session; $updater = $session.CreateUpdateInstaller(); $searcher = $session.CreateUpdateSearcher(); $result = $searcher.Search('IsInstalled=0 and IsHidden=0'); $updater.Updates = $result.Updates; $result = $updater.Install(); Write-Host 'Result: $($result.ResultCode)'; Write-Host 'Updates installed: $($result.Updates.Count)'\"";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            var process = new Process { StartInfo = psi };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(output))
                progress?.Report(output);
            if (!string.IsNullOrWhiteSpace(error))
                progress?.Report("Error: " + error);

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to install updates");
            return -1;
        }
    }
}