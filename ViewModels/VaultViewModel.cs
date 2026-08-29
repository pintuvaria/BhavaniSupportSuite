using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;

namespace BhavaniSupportSuite.ViewModels;

public partial class VaultViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<string> _backupLog = new();

    [ObservableProperty]
    private string _backupDestination = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private ObservableCollection<BackupProfile> _profiles = new()
    {
        new BackupProfile { Name = "User Desktop", Path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop), IsSelected = true },
        new BackupProfile { Name = "User Documents", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), IsSelected = true },
        new BackupProfile { Name = "User Downloads", Path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads", IsSelected = false },
        new BackupProfile { Name = "Chrome Bookmarks", Path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Google\\Chrome\\User Data\\Default\\Bookmarks", IsSelected = true },
        new BackupProfile { Name = "Chrome History", Path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Google\\Chrome\\User Data\\Default\\History", IsSelected = false },
        new BackupProfile { Name = "Network Mappings", Path = "HKCU\\Network", IsSelected = true, IsRegistry = true },
    };

    public VaultViewModel()
    {
        Title = "System Vault";
        StatusMessage = "Ready";
        BackupDestination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "BhavaniBackup");
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (IsBusy) return;

        var selected = Profiles.Where(p => p.IsSelected).ToList();
        if (!selected.Any()) { StatusMessage = "No profiles selected."; return; }

        IsBusy = true;
        ProgressValue = 0;
        BackupLog.Clear();
        StatusMessage = "Creating backup...";

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupDir = Path.Combine(BackupDestination, $"Backup_{timestamp}");

        try
        {
            Directory.CreateDirectory(backupDir);
            BackupLog.Add($"Backup directory: {backupDir}");

            var completed = 0;
            foreach (var profile in selected)
            {
                StatusMessage = $"Backing up: {profile.Name}...";

                if (profile.IsRegistry)
                {
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Exporting registry: {profile.Name}");
                    var regFile = Path.Combine(backupDir, $"{profile.Name.Replace(" ", "_")}.reg");
                    var exitCode = await RunProcessAsync("reg", $"export \"{profile.Path}\" \"{regFile}\" /y");
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Registry export: exit code {exitCode}");
                }
                else if (File.Exists(profile.Path))
                {
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Copying file: {profile.Name}");
                    var destFile = Path.Combine(backupDir, Path.GetFileName(profile.Path));
                    await Task.Run(() => File.Copy(profile.Path, destFile, true));
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] File copied: {destFile}");
                }
                else if (Directory.Exists(profile.Path))
                {
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Copying directory: {profile.Name}");
                    var destDir = Path.Combine(backupDir, profile.Name.Replace(" ", "_"));
                    await Task.Run(() => CopyDirectory(profile.Path, destDir));
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Directory copied: {destDir}");
                }
                else
                {
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Skipped (not found): {profile.Name}");
                }

                completed++;
                ProgressValue = (int)((completed / (double)selected.Count) * 100);
            }

            BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Backup completed successfully.");
            StatusMessage = $"Backup created at: {backupDir}";
        }
        catch (Exception ex)
        {
            BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");
            StatusMessage = $"Backup failed: {ex.Message}";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = "Restore functionality - Select a backup to restore.";
        BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Restore initiated.");
        await Task.Delay(100);
        StatusMessage = "Ready for restore.";
        IsBusy = false;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private async Task<int> RunProcessAsync(string fileName, string arguments)
    {
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName, Arguments = arguments,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            }
        };
        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}

public class BackupProfile
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public bool IsRegistry { get; set; }
}
