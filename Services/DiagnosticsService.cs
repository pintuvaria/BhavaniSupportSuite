using System.Diagnostics;
using System.Text;

namespace BhavaniSupportSuite.Services;

public class DiagnosticsService
{
    public event Action<string>? OutputReceived;
    public event Action<string>? ErrorReceived;
    public event Action<int>? ProcessCompleted;

    public async Task<int> RunCommandAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<int>();
        var process = CreateProcess(fileName, arguments);

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) OutputReceived?.Invoke(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) ErrorReceived?.Invoke(e.Data); };
        process.Exited += (_, _) => { ProcessCompleted?.Invoke(process.ExitCode); tcs.TrySetResult(process.ExitCode); process.Dispose(); };

        try
        {
            cancellationToken.Register(() => { try { process.Kill(); } catch { } tcs.TrySetCanceled(cancellationToken); });
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            ErrorReceived?.Invoke($"Failed to start process: {ex.Message}");
            return -1;
        }
    }

    public async Task<string> RunCommandCapturedAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var output = new StringBuilder();
        var tcs = new TaskCompletionSource<int>();
        var process = CreateProcess(fileName, arguments);

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { output.AppendLine(e.Data); OutputReceived?.Invoke(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) ErrorReceived?.Invoke(e.Data); };
        process.Exited += (_, _) => { tcs.TrySetResult(process.ExitCode); process.Dispose(); };

        try
        {
            cancellationToken.Register(() => { try { process.Kill(); } catch { } tcs.TrySetCanceled(cancellationToken); });
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await tcs.Task;
            return output.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    public static async Task<List<string>> GetSystemInfoAsync()
    {
        var info = new List<string>();
        try
        {
            info.Add($"Hostname: {Environment.MachineName}");
            info.Add($"OS: {Environment.OSVersion}");
            info.Add($"User: {Environment.UserName}");
            info.Add($"Processors: {Environment.ProcessorCount}");
            info.Add($"Architecture: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");

            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            info.Add($"Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s");

            using (var cs = new System.Management.ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                foreach (var obj in cs.Get())
                    info.Add($"Installed RAM: {Convert.ToUInt64(obj["TotalPhysicalMemory"]) / (1024.0 * 1024 * 1024):F1} GB");

            using (var cs = new System.Management.ManagementObjectSearcher("SELECT Name, MaxClockSpeed, NumberOfCores FROM Win32_Processor"))
                foreach (var obj in cs.Get())
                {
                    info.Add($"Processor: {obj["Name"]}");
                    info.Add($"Clock Speed: {Convert.ToUInt32(obj["MaxClockSpeed"]) / 1000.0:F1} GHz");
                    info.Add($"Cores: {obj["NumberOfCores"]}");
                    break;
                }

            using (var cs = new System.Management.ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                foreach (var obj in cs.Get())
                {
                    var vram = Convert.ToUInt64(obj["AdapterRAM"]) / (1024.0 * 1024 * 1024);
                    info.Add($"GPU: {obj["Name"]} ({vram:F1} GB)");
                    break;
                }

            using (var cs = new System.Management.ManagementObjectSearcher("SELECT Manufacturer, Product, SerialNumber FROM Win32_BaseBoard"))
                foreach (var obj in cs.Get())
                {
                    info.Add($"Motherboard: {obj["Manufacturer"]} {obj["Product"]}");
                    info.Add($"Serial: {obj["SerialNumber"]}");
                    break;
                }

            using (var cs = new System.Management.ManagementObjectSearcher("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS"))
                foreach (var obj in cs.Get())
                {
                    info.Add($"BIOS: {obj["SMBIOSBIOSVersion"]}");
                    break;
                }
        }
        catch { info.Add("Unable to retrieve full system info."); }
        return await Task.FromResult(info);
    }

    public static float GetCpuUsage()
    {
        try
        {
            using var c = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            c.NextValue(); Thread.Sleep(100); return c.NextValue();
        }
        catch { return 0f; }
    }

    public static float GetMemoryUsage()
    {
        try { using var c = new PerformanceCounter("Memory", "% Committed Bytes In Use"); return c.NextValue(); }
        catch { return 0f; }
    }

    public static float GetDiskReadBytes()
    {
        try { using var c = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total"); return c.NextValue(); }
        catch { return 0f; }
    }

    public static float GetDiskWriteBytes()
    {
        try { using var c = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total"); return c.NextValue(); }
        catch { return 0f; }
    }

    public static float GetNetworkBytes()
    {
        try
        {
            using var c = new PerformanceCounter("Network Interface", "Bytes Total/sec", "Realtek PCIe GbE Family Controller");
            return c.NextValue();
        }
        catch
        {
            try
            {
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                    if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                        return nic.GetIPv4Statistics().BytesSent + nic.GetIPv4Statistics().BytesReceived;
            }
            catch { }
            return 0f;
        }
    }

    public static float GetCpuTemperature()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (var obj in searcher.Get())
            {
                var rawTemp = Convert.ToUInt32(obj["CurrentTemperature"]);
                return (rawTemp - 2732) / 10.0f;
            }
        }
        catch { }
        return 0f;
    }

    public static List<string> GetDiskHealthInfo()
    {
        var info = new List<string>();
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT Model, Size, MediaType, Status, InterfaceType FROM Win32_DiskDrive");
            foreach (var obj in searcher.Get())
            {
                var size = Convert.ToUInt64(obj["Size"]) / (1024.0 * 1024 * 1024);
                info.Add($"Drive: {obj["Model"]}");
                info.Add($"  Size: {size:F1} GB");
                info.Add($"  Type: {obj["MediaType"]}");
                info.Add($"  Interface: {obj["InterfaceType"]}");
                info.Add($"  Status: {obj["Status"]}");
                info.Add(string.Empty);
            }
        }
        catch { info.Add("Unable to retrieve disk health info."); }
        return info;
    }

    public static List<string> GetBatteryInfo()
    {
        var info = new List<string>();
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_Battery");
            foreach (var obj in searcher.Get())
            {
                info.Add($"Battery: {obj["Name"]}");
                info.Add($"  Status: {obj["Status"]}");
                info.Add($"  Charge: {obj["EstimatedChargeRemaining"]}%");
                info.Add($"  Runtime: {obj["EstimatedRunTime"]} min");
            }
            if (info.Count == 0) info.Add("No battery detected (Desktop system).");
        }
        catch { info.Add("Unable to retrieve battery info."); }
        return info;
    }

    public static async Task<List<string>> GetStartupItemsAsync()
    {
        var items = new List<string>();
        try
        {
            var registryKeys = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
            };

            foreach (var keyPath in registryKeys)
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                    if (key != null)
                        foreach (var name in key.GetValueNames())
                            items.Add($"[{keyPath.Split('\\').Last()}] {name} = {key.GetValue(name)}");
                }
                catch { }
            }

            try
            {
                using var userKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                if (userKey != null)
                    foreach (var name in userKey.GetValueNames())
                        items.Add($"[HKCU\\Run] {name} = {userKey.GetValue(name)}");
            }
            catch { }
        }
        catch { items.Add("Unable to retrieve startup items."); }
        return await Task.FromResult(items);
    }

    public static List<string> GetUsbStorageDevices()
    {
        var devices = new List<string>();
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_USBHub");
            foreach (var obj in searcher.Get())
                devices.Add($"{obj["DeviceID"]} - {obj["Name"]}");
        }
        catch { devices.Add("Unable to enumerate USB devices."); }
        return devices;
    }

    private static Process CreateProcess(string fileName, string arguments)
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };
    }
}
