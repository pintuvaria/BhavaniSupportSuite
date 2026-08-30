using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;

namespace BhavaniSupportSuite.ViewModels;

public partial class StagingViewModel : ViewModelBase
{
    private readonly DiagnosticsService _diagnostics;

    [ObservableProperty]
    private ObservableCollection<string> _installLog = new();

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _installStatus;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private ObservableCollection<PackageItem> _packages = new()
    {
        new PackageItem { Name = "Google Chrome", SilentArgs = "/silent /install", IsSelected = true, Category = "Browser" },
        new PackageItem { Name = "Mozilla Firefox", SilentArgs = "/S", IsSelected = false, Category = "Browser" },
        new PackageItem { Name = "7-Zip", SilentArgs = "/S", IsSelected = true, Category = "Utility" },
        new PackageItem { Name = "WinRAR", SilentArgs = "/S", IsSelected = false, Category = "Utility" },
        new PackageItem { Name = "VS Code", SilentArgs = "/verysilent /mergetasks=!runcode", IsSelected = true, Category = "Development" },
        new PackageItem { Name = "Notepad++", SilentArgs = "/S", IsSelected = false, Category = "Development" },
        new PackageItem { Name = "Adobe Acrobat Reader", SilentArgs = "/S /msi EULA_ACCEPT=YES", IsSelected = false, Category = "Productivity" },
        new PackageItem { Name = "Microsoft Teams", SilentArgs = "/s", IsSelected = false, Category = "Communication" },
        new PackageItem { Name = "PuTTY", SilentArgs = "/S", IsSelected = false, Category = "Network" },
        new PackageItem { Name = "FileZilla", SilentArgs = "/S", IsSelected = false, Category = "Network" },
    };

    [ObservableProperty]
    private ObservableCollection<TweakItem> _registryTweaks = new()
    {
        new TweakItem { Name = "Disable Telemetry", IsSelected = true, Category = "Privacy" },
        new TweakItem { Name = "Show File Extensions", IsSelected = true, Category = "Explorer" },
        new TweakItem { Name = "Enable Remote Desktop", IsSelected = false, Category = "Remote" },
        new TweakItem { Name = "Disable Cortana", IsSelected = true, Category = "Privacy" },
        new TweakItem { Name = "Enable Dark Mode", IsSelected = true, Category = "Personalization" },
        new TweakItem { Name = "Disable Lock Screen", IsSelected = false, Category = "Personalization" },
        new TweakItem { Name = "Show Hidden Files", IsSelected = true, Category = "Explorer" },
        new TweakItem { Name = "Disable AutoPlay", IsSelected = true, Category = "Security" },
        new TweakItem { Name = "Disable Windows Defender Sample Submission", IsSelected = false, Category = "Security" },
        new TweakItem { Name = "Enable Hibernate", IsSelected = false, Category = "Power" },
    };

    [ObservableProperty]
    private string _defenderStatus = "Unknown";

    public StagingViewModel(DiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
        Title = "Provisioning & Staging";
        _installStatus = "Ready";
        _ = CheckDefenderStatusAsync();
    }

    private async Task CheckDefenderStatusAsync()
    {
        try
        {
            var output = await _diagnostics.RunCommandCapturedAsync("powershell", "-Command \"Get-MpComputerStatus | Select-RealTimeProtectionEnabled\"");
            DefenderStatus = output.Contains("True") ? "Active" : "Inactive";
        }
        catch { DefenderStatus = "Unknown"; }
    }

    [RelayCommand]
    private async Task InstallSelectedAsync()
    {
        if (IsInstalling) return;

        var selected = Packages.Where(p => p.IsSelected).ToList();
        if (!selected.Any()) { InstallStatus = "No packages selected."; return; }

        IsInstalling = true;
        ProgressValue = 0;
        InstallLog.Clear();

        var completed = 0;
        foreach (var package in selected)
        {
            InstallStatus = $"Installing: {package.Name}...";
            InstallLog.Add($"[{DateTime.Now:HH:mm:ss}] Starting installation: {package.Name}");

            _diagnostics.OutputReceived += msg => SafeDispatch(() => InstallLog.Add($"  {msg}"));
            _diagnostics.ErrorReceived += msg => SafeDispatch(() => InstallLog.Add($"  [ERR] {msg}"));

            var exitCode = await _diagnostics.RunCommandAsync("cmd", $"/c winget install --id {package.WingetId} {package.SilentArgs} --accept-source-agreements --accept-package-agreements");
            InstallLog.Add($"[{DateTime.Now:HH:mm:ss}] {package.Name} completed with exit code: {exitCode}");

            completed++;
            ProgressValue = (int)((completed / (double)selected.Count) * 100);
        }

        InstallStatus = $"Installation complete. {completed}/{selected.Count} packages installed.";
        IsInstalling = false;
    }

    [RelayCommand]
    private async Task ApplyRegistryTweaksAsync()
    {
        var selected = RegistryTweaks.Where(t => t.IsSelected).ToList();
        if (!selected.Any()) return;

        foreach (var tweak in selected)
        {
            InstallLog.Add($"[{DateTime.Now:HH:mm:ss}] Applying tweak: {tweak.Name}");

            var command = tweak.Name switch
            {
                "Disable Telemetry" => @"reg add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\DataCollection"" /v AllowTelemetry /t REG_DWORD /d 0 /f",
                "Show File Extensions" => @"reg add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"" /v HideFileExt /t REG_DWORD /d 0 /f",
                "Enable Remote Desktop" => @"reg add ""HKLM\SYSTEM\CurrentControlSet\Control\Terminal Server"" /v fDenyTSConnections /t REG_DWORD /d 0 /f",
                "Disable Cortana" => @"reg add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\Windows Search"" /v AllowCortana /t REG_DWORD /d 0 /f",
                "Enable Dark Mode" => @"reg add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"" /v AppsUseLightTheme /t REG_DWORD /d 0 /f",
                "Disable Lock Screen" => @"reg add ""HKLM\SOFTWARE\Policies\Microsoft\Windows\Personalization"" /v NoLockScreen /t REG_DWORD /d 1 /f",
                "Show Hidden Files" => @"reg add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"" /v Hidden /t REG_DWORD /d 1 /f",
                "Disable AutoPlay" => @"reg add ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer"" /v NoDriveTypeAutoRun /t REG_DWORD /d 255 /f",
                _ => ""
            };

            if (!string.IsNullOrEmpty(command))
            {
                var exitCode = await _diagnostics.RunCommandAsync("reg", command.Replace("reg ", ""));
                InstallLog.Add($"[{DateTime.Now:HH:mm:ss}] {tweak.Name} - Exit code: {exitCode}");
            }
        }

        InstallStatus = "Registry tweaks applied.";
    }
}

public class PackageItem
{
    public string Name { get; set; } = string.Empty;
    public string SilentArgs { get; set; } = string.Empty;
    public string WingetId { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class TweakItem
{
    public string Name { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public string Category { get; set; } = string.Empty;
}
