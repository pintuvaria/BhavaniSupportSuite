using System.Diagnostics;
using System.IO;
using System.Management;
using Microsoft.Win32;

namespace BhavaniSupportSuite.Services;

public class ProcessInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public double CpuUsage { get; set; }
    public long MemoryMb { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsSigned { get; set; }
}

public class StartupItem
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

public class ScheduledTaskInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string NextRun { get; set; } = string.Empty;
}

public class SecurityService
{
    public static List<ProcessInfo> GetRunningProcesses()
    {
        var processes = new List<ProcessInfo>();
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var memMb = proc.WorkingSet64 / (1024 * 1024);
                    var filePath = string.Empty;
                    try { filePath = proc.MainModule?.FileName ?? string.Empty; } catch { }

                    processes.Add(new ProcessInfo
                    {
                        Id = proc.Id,
                        Name = proc.ProcessName,
                        FilePath = filePath,
                        MemoryMb = memMb,
                        Status = "Running"
                    });
                }
                catch { }
            }
        }
        catch { }
        return processes.OrderByDescending(p => p.MemoryMb).ToList();
    }

    public static async Task<List<StartupItem>> GetStartupItemsAsync()
    {
        var items = new List<StartupItem>();

        var registryPaths = new (string Key, string Location)[]
        {
            (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "HKLM\\Run"),
            (@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM\\RunOnce"),
            (@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM\\WOW6432Node\\Run"),
        };

        foreach (var (keyPath, location) in registryPaths)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                if (key != null)
                    foreach (var name in key.GetValueNames())
                        items.Add(new StartupItem
                        {
                            Name = name,
                            Command = key.GetValue(name)?.ToString() ?? "",
                            Location = location,
                            IsEnabled = true
                        });
            }
            catch { }
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            if (key != null)
                foreach (var name in key.GetValueNames())
                    items.Add(new StartupItem
                    {
                        Name = name,
                        Command = key.GetValue(name)?.ToString() ?? "",
                        Location = "HKCU\\Run",
                        IsEnabled = true
                    });
        }
        catch { }

        try
        {
            var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(startupFolder))
            {
                foreach (var file in Directory.GetFiles(startupFolder))
                    items.Add(new StartupItem
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Command = file,
                        Location = "Startup Folder",
                        IsEnabled = true
                    });
            }
        }
        catch { }

        return await Task.FromResult(items);
    }

    public static List<ScheduledTaskInfo> GetScheduledTasks()
    {
        var tasks = new List<ScheduledTaskInfo>();
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks",
                    Arguments = "/query /fo CSV /nh",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                if (parts.Length >= 3)
                {
                    var name = parts[0].Trim('"', ' ');
                    var path = parts[1].Trim('"', ' ');
                    var state = parts[2].Trim('"', ' ');
                    tasks.Add(new ScheduledTaskInfo
                    {
                        Name = name,
                        Path = path,
                        State = state,
                        NextRun = parts.Length > 3 ? parts[3].Trim('"', ' ') : "N/A"
                    });
                }
            }
        }
        catch { }
        return tasks;
    }

    public static bool GetBitLockerStatus()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "manage-bde",
                    Arguments = "-status C:",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output.Contains("Protection Status: Protection On");
        }
        catch { }
        return false;
    }

    public static bool GetUsbStorageEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\USBSTOR");
            if (key != null)
            {
                var value = key.GetValue("Start");
                if (value != null)
                    return Convert.ToInt32(value) == 3;
            }
        }
        catch { }
        return true;
    }

    public static void SetUsbStorageEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\USBSTOR", true);
            key?.SetValue("Start", enabled ? 3 : 4, RegistryValueKind.DWord);
        }
        catch { }
    }

    public static Task<string> KillProcessAsync(int processId)
    {
        try
        {
            var proc = Process.GetProcessById(processId);
            proc.Kill();
            return Task.FromResult($"Process {processId} ({proc.ProcessName}) terminated.");
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Failed to kill process: {ex.Message}");
        }
    }

    public static List<string> GetUnsignedBinaries()
    {
        var unsigned = new List<string>();
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    var path = proc.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        try
                        {
                            var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path);
                            _ = cert;
                        }
                        catch
                        {
                            unsigned.Add($"{proc.ProcessName} ({path})");
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        return unsigned.Distinct().ToList();
    }
}
