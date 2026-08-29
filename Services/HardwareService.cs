using System.Management;
using System.Text;

namespace BhavaniSupportSuite.Services;

public class HardwareInfo
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class SmartStatus
{
    public string DriveName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Temperature { get; set; }
    public long TotalSectors { get; set; }
    public string HealthPercent { get; set; } = string.Empty;
}

public class HardwareService
{
    public static List<HardwareInfo> GetDetailedHardwareInfo()
    {
        var info = new List<HardwareInfo>();

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (var obj in cs.Get())
            {
                info.Add(new HardwareInfo { Name = "Processor", Value = obj["Name"]?.ToString() ?? "N/A", Category = "CPU" });
                info.Add(new HardwareInfo { Name = "Max Clock Speed", Value = $"{Convert.ToUInt32(obj["MaxClockSpeed"]) / 1000.0:F2} GHz", Category = "CPU" });
                info.Add(new HardwareInfo { Name = "Current Clock Speed", Value = $"{Convert.ToUInt32(obj["CurrentClockSpeed"]) / 1000.0:F2} GHz", Category = "CPU" });
                info.Add(new HardwareInfo { Name = "Cores", Value = obj["NumberOfCores"]?.ToString() ?? "N/A", Category = "CPU" });
                info.Add(new HardwareInfo { Name = "Logical Processors", Value = obj["NumberOfLogicalProcessors"]?.ToString() ?? "N/A", Category = "CPU" });
                info.Add(new HardwareInfo { Name = "Architecture", Value = obj["Architecture"]?.ToString() ?? "N/A", Category = "CPU" });
                break;
            }

            using var mb = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (var obj in mb.Get())
            {
                info.Add(new HardwareInfo { Name = "Motherboard", Value = $"{obj["Manufacturer"]} {obj["Product"]}", Category = "Motherboard" });
                info.Add(new HardwareInfo { Name = "Serial Number", Value = obj["SerialNumber"]?.ToString() ?? "N/A", Category = "Motherboard" });
                break;
            }

            using var bios = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (var obj in bios.Get())
            {
                info.Add(new HardwareInfo { Name = "BIOS Version", Value = obj["SMBIOSBIOSVersion"]?.ToString() ?? "N/A", Category = "BIOS" });
                info.Add(new HardwareInfo { Name = "BIOS Manufacturer", Value = obj["Manufacturer"]?.ToString() ?? "N/A", Category = "BIOS" });
                break;
            }

            using var ram = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
            var slot = 1;
            foreach (var obj in ram.Get())
            {
                var capacity = Convert.ToUInt64(obj["Capacity"]) / (1024.0 * 1024);
                var speed = Convert.ToUInt32(obj["Speed"]);
                info.Add(new HardwareInfo { Name = $"Slot {slot}", Value = $"{capacity:F0} MB DDR4 @ {speed} MHz ({obj["Manufacturer"]})", Category = "RAM" });
                slot++;
            }

            using var gpu = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (var obj in gpu.Get())
            {
                var vram = Convert.ToUInt64(obj["AdapterRAM"]) / (1024.0 * 1024 * 1024);
                info.Add(new HardwareInfo { Name = "GPU", Value = obj["Name"]?.ToString() ?? "N/A", Category = "GPU" });
                info.Add(new HardwareInfo { Name = "VRAM", Value = $"{vram:F1} GB", Category = "GPU" });
                info.Add(new HardwareInfo { Name = "Driver Version", Value = obj["DriverVersion"]?.ToString() ?? "N/A", Category = "GPU" });
                break;
            }
        }
        catch { info.Add(new HardwareInfo { Name = "Error", Value = "Unable to retrieve hardware info", Category = "System" }); }

        return info;
    }

    public static List<SmartStatus> GetSmartStatus()
    {
        var drives = new List<SmartStatus>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            foreach (var obj in searcher.Get())
            {
                drives.Add(new SmartStatus
                {
                    DriveName = obj["Model"]?.ToString() ?? "Unknown",
                    Status = obj["Status"]?.ToString() ?? "Unknown",
                    Temperature = 0,
                    TotalSectors = Convert.ToInt64(obj["Size"]) / 512,
                    HealthPercent = string.Equals(obj["Status"]?.ToString(), "OK", StringComparison.Ordinal) ? "Healthy" : "Warning"
                });
            }

            try
            {
                using var thermalSearcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                foreach (var obj in thermalSearcher.Get())
                {
                    var temp = (int)((Convert.ToUInt32(obj["CurrentTemperature"]) - 2732) / 10);
                    foreach (var drive in drives)
                        drive.Temperature = temp;
                    break;
                }
            }
            catch { }
        }
        catch { }
        return drives;
    }

    public static List<string> GetBatteryDetails()
    {
        var info = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
            foreach (var obj in searcher.Get())
            {
                info.Add($"Battery Name: {obj["Name"]}");
                info.Add($"Status: {obj["Status"]}");
                info.Add($"Charge Level: {obj["EstimatedChargeRemaining"]}%");
                info.Add($"Estimated Runtime: {obj["EstimatedRunTime"]} minutes");
                info.Add($"Chemistry: {obj["Chemistry"]}");
            }
            if (info.Count == 0) info.Add("No battery detected (Desktop system).");
        }
        catch { info.Add("Unable to retrieve battery info."); }
        return info;
    }

    public static float GetCpuTemperature()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (var obj in searcher.Get())
                return (Convert.ToUInt32(obj["CurrentTemperature"]) - 2732) / 10.0f;
        }
        catch { }
        return 0f;
    }

    public static float GetFanSpeed()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Fan");
            foreach (var obj in searcher.Get())
                return Convert.ToUInt32(obj["ActiveCooling"]) > 0 ? 100f : 0f;
        }
        catch { }
        return 0f;
    }

    public static async Task<List<string>> RunMemoryDiagnosticAsync()
    {
        var results = new List<string>();
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "mdsched.exe",
                    Arguments = "/check",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();
            results.Add(output);
        }
        catch { results.Add("Memory diagnostic tool not available."); }
        return results;
    }
}
