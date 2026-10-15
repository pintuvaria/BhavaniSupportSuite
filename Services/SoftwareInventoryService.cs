using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using BhavaniSupportSuite.Core;
using Serilog;

namespace BhavaniSupportSuite.Services;

public class InstalledSoftware
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string InstallDate { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string UninstallString { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public long EstimatedSize { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class SoftwareInventoryService
{
    public async Task<List<InstalledSoftware>> GetInstalledSoftwareAsync()
    {
        var software = new List<InstalledSoftware>();

        await GetFromRegistryAsync(software, Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM");
        await GetFromRegistryAsync(software, Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "HKLM (Wow6432)");
        await GetFromRegistryAsync(software, Microsoft.Win32.Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "HKCU");
        await GetFromWmiAsync(software);

        var unique = new Dictionary<string, InstalledSoftware>();
        foreach (var app in software)
        {
            var key = app.Name + "|" + app.Version;
            if (!unique.ContainsKey(key) || string.IsNullOrEmpty(unique[key].InstallDate))
                unique[key] = app;
        }

        return unique.Values
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task GetFromRegistryAsync(List<InstalledSoftware> list, Microsoft.Win32.RegistryKey rootKey, string subKey, string source)
    {
        try
        {
            using var key = rootKey.OpenSubKey(subKey);
            if (key == null) return;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                try
                {
                    using var subKeyObj = key.OpenSubKey(subKeyName);
                    if (subKeyObj == null) continue;

                    var name = subKeyObj.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var systemComponent = subKeyObj.GetValue("SystemComponent");
                    if (systemComponent != null && Convert.ToInt32(systemComponent) == 1) continue;

                    var app = new InstalledSoftware
                    {
                        Name = name,
                        Version = subKeyObj.GetValue("DisplayVersion")?.ToString() ?? string.Empty,
                        Publisher = subKeyObj.GetValue("Publisher")?.ToString() ?? string.Empty,
                        InstallDate = subKeyObj.GetValue("InstallDate")?.ToString() ?? string.Empty,
                        InstallLocation = subKeyObj.GetValue("InstallLocation")?.ToString() ?? string.Empty,
                        UninstallString = subKeyObj.GetValue("UninstallString")?.ToString() ?? string.Empty,
                        Architecture = subKeyObj.GetValue("WindowsInstaller") != null ? "x64" :
                                     subKeyObj.GetValue("EstimatedSize") != null ? "x86" : "Unknown",
                        Source = source
                    };

                    if (long.TryParse(subKeyObj.GetValue("EstimatedSize")?.ToString(), out var size))
                        app.EstimatedSize = size * 1024;

                    list.Add(app);
                }
                catch { }
            }
        }
        catch { }
        await Task.CompletedTask;
    }

    private async Task GetFromWmiAsync(List<InstalledSoftware> list)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Product");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var app = new InstalledSoftware
                {
                    Name = name,
                    Version = obj["Version"]?.ToString() ?? string.Empty,
                    Publisher = obj["Vendor"]?.ToString() ?? string.Empty,
                    InstallDate = obj["InstallDate"]?.ToString() ?? string.Empty,
                    InstallLocation = obj["InstallLocation"]?.ToString() ?? string.Empty,
                    UninstallString = "msiexec /x " + (obj["IdentifyingNumber"]?.ToString() ?? string.Empty),
                    Source = "WMI (MSI)"
                };
                list.Add(app);
            }
        }
        catch { }
        await Task.CompletedTask;
    }

    public async Task<bool> UninstallSoftwareAsync(string uninstallString)
    {
        if (string.IsNullOrWhiteSpace(uninstallString)) return false;

        try
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo();
            process.StartInfo.FileName = "cmd";
            process.StartInfo.Arguments = "/c " + uninstallString;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;
            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    public async Task<List<InstalledSoftware>> SearchSoftwareAsync(string query)
    {
        var all = await GetInstalledSoftwareAsync();
        if (string.IsNullOrWhiteSpace(query)) return all;

        var lowerQuery = query.ToLowerInvariant();
        return all.Where(x =>
            x.Name.ToLower().Contains(lowerQuery) ||
            x.Publisher.ToLower().Contains(lowerQuery) ||
            x.Version.Contains(query)).ToList();
    }
}