using System.Diagnostics;
using System.IO;

namespace BhavaniSupportSuite.Services;

public class CleanResult
{
    public string Location { get; set; } = string.Empty;
    public long BytesFreed { get; set; }
    public int FilesDeleted { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DriverInfo
{
    public string Name { get; set; } = string.Empty;
    public string InfName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
}

public class StorageService
{
    public static async Task<List<CleanResult>> RunDeepCleanAsync(IProgress<string>? progress = null)
    {
        var results = new List<CleanResult>();

        var cleanLocations = new (string Name, string Path)[]
        {
            ("Windows Temp", Path.GetTempPath()),
            ("Windows Temp (System)", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")),
            ("SoftwareDistribution", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download")),
            ("Prefetch", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch")),
            ("Windows Logs", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs")),
            ("Windows Installer Cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Installer", "$PatchCache$")),
        };

        foreach (var (name, path) in cleanLocations)
        {
            progress?.Report($"Cleaning {name}...");
            var result = await CleanDirectoryAsync(name, path);
            results.Add(result);
            progress?.Report($"  {name}: {result.FilesDeleted} files, {result.BytesFreed / 1024.0 / 1024:F2} MB freed");
        }

        try
        {
            progress?.Report("Running Disk Cleanup...");
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cleanmgr",
                    Arguments = "/sagerun:1",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            results.Add(new CleanResult { Location = "Disk Cleanup", Success = true, Message = "Completed" });
        }
        catch (Exception ex)
        {
            results.Add(new CleanResult { Location = "Disk Cleanup", Success = false, Message = ex.Message });
        }

        return results;
    }

    private static async Task<CleanResult> CleanDirectoryAsync(string name, string path)
    {
        var result = new CleanResult { Location = name };
        try
        {
            if (!Directory.Exists(path))
            {
                result.Message = "Directory not found";
                return result;
            }

            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            long totalBytes = 0;
            int deletedCount = 0;

            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    totalBytes += info.Length;
                    File.Delete(file);
                    deletedCount++;
                }
                catch { }
            }

            result.BytesFreed = totalBytes;
            result.FilesDeleted = deletedCount;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Message = ex.Message;
        }

        return await Task.FromResult(result);
    }

    public static async Task<List<DriverInfo>> GetActiveDriversAsync()
    {
        var drivers = new List<DriverInfo>();
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pnputil",
                    Arguments = "/enum-drivers",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();

            var blocks = output.Split(new[] { "Published Name:" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var block in blocks)
            {
                var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var driver = new DriverInfo();
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Original Name:"))
                        driver.Name = trimmed.Replace("Original Name:", "").Trim();
                    if (trimmed.StartsWith("Provider Name:"))
                        driver.Provider = trimmed.Replace("Provider Name:", "").Trim();
                    if (trimmed.StartsWith("Driver Version:"))
                        driver.Version = trimmed.Replace("Driver Version:", "").Trim();
                    if (trimmed.StartsWith("Class Name:"))
                        driver.Class = trimmed.Replace("Class Name:", "").Trim();
                    if (trimmed.Contains(".inf"))
                        driver.InfName = trimmed.Split(new[] { ':' }, 2).Last().Trim();
                }
                if (!string.IsNullOrEmpty(driver.InfName))
                    drivers.Add(driver);
            }
        }
        catch { }
        return drivers;
    }

    public static async Task<bool> BackupDriversAsync(string destinationFolder)
    {
        try
        {
            Directory.CreateDirectory(destinationFolder);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pnputil",
                    Arguments = $"/export-driver * /destination \"{destinationFolder}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    public static async Task<long> GetDirectorySizeAsync(string path)
    {
        return await Task.Run(() =>
        {
            try
            {
                return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length);
            }
            catch { return 0L; }
        });
    }

    public static List<string> GetDiskSpaceInfo()
    {
        var info = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    var totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                    var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                    var usedPercent = ((totalGb - freeGb) / totalGb) * 100;
                    info.Add($"{drive.Name} {drive.VolumeLabel}");
                    info.Add($"  Total: {totalGb:F1} GB");
                    info.Add($"  Free: {freeGb:F1} GB");
                    info.Add($"  Used: {usedPercent:F1}%");
                    info.Add(string.Empty);
                }
            }
        }
        catch { info.Add("Unable to retrieve disk info."); }
        return info;
    }
}
