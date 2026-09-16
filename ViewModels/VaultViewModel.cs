using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using Serilog;

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
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private ObservableCollection<BackupProfile> _profiles = new()
    {
        new BackupProfile { Name = "User Desktop", Path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop), IsSelected = true },
        new BackupProfile { Name = "User Documents", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), IsSelected = true },
        new BackupProfile { Name = "User Downloads", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), IsSelected = false },
        new BackupProfile { Name = "Chrome Bookmarks", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data", "Default", "Bookmarks"), IsSelected = true },
        new BackupProfile { Name = "Chrome History", Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data", "Default", "History"), IsSelected = false },
        new BackupProfile { Name = "Network Mappings", Path = "HKCU\\Network", IsSelected = true, IsRegistry = true },
    };

    public VaultViewModel()
    {
        Title = "System Vault";
        StatusMessage = "Ready";
        BackupDestination = App.Settings?.BackupDestination ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "BhavaniBackup");
    }

    partial void OnBackupDestinationChanged(string value)
    {
        if (App.Settings != null)
        {
            App.Settings.BackupDestination = value;
            SettingsService.Save(App.Settings);
        }
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

            var manifest = new List<BackupManifestEntry>();
            var completed = 0;

            foreach (var profile in selected)
            {
                StatusMessage = $"Backing up: {profile.Name}...";

                if (profile.IsRegistry)
                {
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Exporting registry: {profile.Name}");
                    var regFile = Path.Combine(backupDir, $"{SanitizeFileName(profile.Name)}.reg");
                    var sanitizedPath = SecurityUtilities.SanitizeArgument(profile.Path);
                    var sanitizedRegFile = SecurityUtilities.SanitizePath(regFile);
                    var exitCode = await RunProcessAsync("reg", $"export \"{sanitizedPath}\" \"{sanitizedRegFile}\" /y");
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Registry export: exit code {exitCode}");
                    
                    if (exitCode == 0 && File.Exists(regFile))
                    {
                        var checksum = SecurityUtilities.ComputeSha256(regFile);
                        manifest.Add(new BackupManifestEntry
                        {
                            Name = profile.Name,
                            Type = "Registry",
                            SourcePath = profile.Path,
                            BackupPath = regFile,
                            SizeBytes = new FileInfo(regFile).Length,
                            Sha256 = checksum,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                else if (File.Exists(profile.Path))
                {
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Copying file: {profile.Name}");
                    var destFile = Path.Combine(backupDir, SanitizeFileName(Path.GetFileName(profile.Path)));
                    await Task.Run(() => File.Copy(profile.Path, destFile, true));
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] File copied: {destFile}");

                    if (File.Exists(destFile))
                    {
                        var checksum = SecurityUtilities.ComputeSha256(destFile);
                        manifest.Add(new BackupManifestEntry
                        {
                            Name = profile.Name,
                            Type = "File",
                            SourcePath = profile.Path,
                            BackupPath = destFile,
                            SizeBytes = new FileInfo(destFile).Length,
                            Sha256 = checksum,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                else if (Directory.Exists(profile.Path))
                {
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Copying directory: {profile.Name}");
                    var destDir = Path.Combine(backupDir, SanitizeFileName(profile.Name));
                    await Task.Run(() => CopyDirectory(profile.Path, destDir));
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Directory copied: {destDir}");

                    // Compute checksums for all files in directory
                    var dirFiles = Directory.GetFiles(destDir, "*", SearchOption.AllDirectories);
                    foreach (var file in dirFiles)
                    {
                        var relativePath = file.Substring(destDir.Length + 1);
                        var checksum = SecurityUtilities.ComputeSha256(file);
                        manifest.Add(new BackupManifestEntry
                        {
                            Name = $"{profile.Name}/{relativePath}",
                            Type = "Directory File",
                            SourcePath = profile.Path,
                            BackupPath = file,
                            SizeBytes = new FileInfo(file).Length,
                            Sha256 = checksum,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                else
                {
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Skipped (not found): {profile.Name}");
                }

                completed++;
                ProgressValue = (int)((completed / (double)selected.Count) * 100);
            }

            // Save manifest with checksums
            var manifestPath = Path.Combine(backupDir, "manifest.json");
            var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, manifestJson);
            BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Manifest saved with {manifest.Count} entries.");

            BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Backup completed successfully.");
            StatusMessage = $"Backup created at: {backupDir}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Backup failed");
            BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");
            StatusMessage = $"Backup failed: {ex.Message}";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task VerifyBackupAsync()
    {
        if (IsBusy) return;
        
        // Simple file dialog to select backup directory
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Manifest files (*.json)|*.json",
            Title = "Select backup manifest to verify"
        };

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        StatusMessage = "Verifying backup integrity...";
        BackupLog.Clear();

        try
        {
            var manifestJson = await File.ReadAllTextAsync(dialog.FileName);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<List<BackupManifestEntry>>(manifestJson) ?? new List<BackupManifestEntry>();
            var backupDir = Path.GetDirectoryName(dialog.FileName)!;

            int verified = 0, failed = 0, missing = 0;

            foreach (var entry in manifest)
            {
                var fullPath = Path.Combine(backupDir, Path.GetFileName(entry.BackupPath));
                
                if (!File.Exists(fullPath))
                {
                    BackupLog.Add($"[MISSING] {entry.Name}");
                    missing++;
                    continue;
                }

                var actualChecksum = SecurityUtilities.ComputeSha256(fullPath);
                if (string.Equals(actualChecksum, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    BackupLog.Add($"[OK] {entry.Name}");
                    verified++;
                }
                else
                {
                    BackupLog.Add($"[MISMATCH] {entry.Name} (expected: {entry.Sha256}, got: {actualChecksum})");
                    failed++;
                }
            }

            BackupLog.Add($"Verification complete: {verified} OK, {failed} mismatched, {missing} missing.");
            StatusMessage = $"Verification complete: {verified} OK, {failed} failed, {missing} missing";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Backup verification failed");
            BackupLog.Add($"Error: {ex.Message}");
            StatusMessage = "Verification failed";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (IsBusy) return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Manifest files (*.json)|*.json",
            Title = "Select backup manifest to restore"
        };

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        StatusMessage = "Restoring backup...";
        BackupLog.Clear();

        try
        {
            var manifestJson = await File.ReadAllTextAsync(dialog.FileName);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<List<BackupManifestEntry>>(manifestJson) ?? new List<BackupManifestEntry>();
            var backupDir = Path.GetDirectoryName(dialog.FileName)!;
            var restoreDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "BhavaniRestore", DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            Directory.CreateDirectory(restoreDir);
            BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Restoring to: {restoreDir}");

            int restored = 0, failed = 0;

            foreach (var entry in manifest)
            {
                var sourcePath = Path.Combine(backupDir, Path.GetFileName(entry.BackupPath));
                
                if (!File.Exists(sourcePath))
                {
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] [MISSING] {entry.Name}");
                    failed++;
                    continue;
                }

                var actualChecksum = SecurityUtilities.ComputeSha256(sourcePath);
                if (!string.Equals(SecurityUtilities.ComputeSha256(sourcePath), entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] [MISMATCH] {entry.Name} - integrity check failed");
                    failed++;
                    continue;
                }

                try
                {
                    var destPath = Path.Combine(restoreDir, entry.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    
                    if (entry.Type == "Registry")
                    {
                        var exitCode = await RunProcessAsync("reg", $"import \"{sourcePath}\"");
                        BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Restored registry: {entry.Name} (exit: {exitCode})");
                    }
                    else
                    {
                        var destPath2 = Path.Combine(restoreDir, Path.GetFileName(entry.BackupPath));
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath2)!);
                        await Task.Run(() => File.Copy(sourcePath, destPath2, true));
                        BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Restored file: {entry.Name}");
                    }
                    
                    restored++;
                }
                catch (Exception ex)
                {
                    BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Failed to restore {entry.Name}: {ex.Message}");
                    failed++;
                }
            }

            BackupLog.Add($"[{DateTime.Now:HH:mm:ss}] Restore complete: {restored} restored, {failed} failed.");
            StatusMessage = $"Restore complete: {restored} restored, {failed} failed to {restoreDir}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Restore failed");
            BackupLog.Add($"Error: {ex.Message}");
            StatusMessage = "Restore failed: " + ex.Message;
        }

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

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name);
        foreach (var c in invalidChars)
        {
            sb.Replace(c, '_');
        }
        return sb.ToString();
    }

    private async Task<int> RunProcessAsync(string fileName, string arguments)
    {
        var sanitizedFile = SecurityUtilities.SanitizePath(fileName);
        var sanitizedArgs = SecurityUtilities.SanitizeArgument(arguments);
        
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = sanitizedFile, 
                Arguments = SecurityUtilities.SanitizeArgument(arguments),
                UseShellExecute = false, 
                CreateNoWindow = true,
                RedirectStandardOutput = true, 
                RedirectStandardError = true
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

public class BackupManifestEntry
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}