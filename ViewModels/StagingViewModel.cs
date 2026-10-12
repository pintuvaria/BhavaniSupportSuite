using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;
using Serilog;

namespace BhavaniSupportSuite.ViewModels;

public partial class StagingViewModel : ViewModelBase
{
    private readonly DiagnosticsService _diagnostics;
    private CancellationTokenSource? _cancellationTokenSource;
    private Action<string>? _outputHandler;
    private Action<string>? _errorHandler;

    [ObservableProperty]
    private ObservableCollection<string> _installLog = new();

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private string _installStatus = string.Empty;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private ObservableCollection<PackageItem> _packages = new()
    {
        new PackageItem { Name = "Google Chrome", SilentArgs = "/silent /install", WingetId = "Google.Chrome", IsSelected = true, Category = "Browser" },
        new PackageItem { Name = "Mozilla Firefox", SilentArgs = "/S", WingetId = "Mozilla.Firefox", IsSelected = false, Category = "Browser" },
        new PackageItem { Name = "7-Zip", SilentArgs = "/S", WingetId = "7zip.7zip", IsSelected = true, Category = "Utility" },
        new PackageItem { Name = "WinRAR", SilentArgs = "/S", WingetId = "RARLab.WinRAR", IsSelected = false, Category = "Utility" },
        new PackageItem { Name = "VS Code", SilentArgs = "/verysilent /mergetasks=!runcode", WingetId = "Microsoft.VisualStudioCode", IsSelected = true, Category = "Development" },
        new PackageItem { Name = "Notepad++", SilentArgs = "/S", WingetId = "Notepad++.Notepad++", IsSelected = false, Category = "Development" },
        new PackageItem { Name = "Adobe Acrobat Reader", SilentArgs = "/S /msi EULA_ACCEPT=YES", WingetId = "Adobe.AdobeAcrobatReader", IsSelected = false, Category = "Productivity" },
        new PackageItem { Name = "Microsoft Teams", SilentArgs = "/s", WingetId = "Microsoft.Teams", IsSelected = false, Category = "Communication" },
        new PackageItem { Name = "PuTTY", SilentArgs = "/S", WingetId = "PuTTY.PuTTY", IsSelected = false, Category = "Network" },
        new PackageItem { Name = "FileZilla", SilentArgs = "/S", WingetId = "FileZilla.Client", IsSelected = false, Category = "Network" },
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

        // Validate all WingetIds before starting
        foreach (var pkg in selected)
        {
            if (!SecurityUtilities.IsSafeArgument(pkg.WingetId) || !SecurityUtilities.IsSafeArgument(pkg.SilentArgs))
            {
                InstallStatus = $"Invalid package data for {pkg.Name}";
                return;
            }
        }

        _cancellationTokenSource = new CancellationTokenSource();
        IsInstalling = true;
        ProgressValue = 0;
        InstallLog.Clear();
        InstallStatus = "Starting installation...";

        // Subscribe to output events
        _outputHandler = msg => SafeDispatch(() => InstallLog.Add($"  {msg}"));
        _errorHandler = msg => SafeDispatch(() => InstallLog.Add($"  [ERR] {msg}"));

        _diagnostics.OutputReceived += _outputHandler;
        _diagnostics.ErrorReceived += _errorHandler;

        try
        {
            var completed = 0;
            foreach (var package in selected)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested) break;

                InstallStatus = $"Installing: {package.Name}...";
                InstallLog.Add($"[{DateTime.Now:HH:mm:ss}] Starting installation: {package.Name}");

                var wingetId = SecurityUtilities.SanitizeArgument(package.WingetId);
                var silentArgs = SecurityUtilities.SanitizeArgument(package.SilentArgs);

                var exitCode = await _diagnostics.RunCommandAsync("cmd", $"/c winget install --id {wingetId} {silentArgs} --accept-source-agreements --accept-package-agreements", _cancellationTokenSource.Token);
                
                if (_cancellationTokenSource.Token.IsCancellationRequested) break;

                InstallLog.Add($"[{DateTime.Now:HH:mm:ss}] {package.Name} completed with exit code: {exitCode}");

                completed++;
                ProgressValue = (int)((completed / (double)selected.Count) * 100);
            }

            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                InstallStatus = "Installation cancelled.";
            }
            else
            {
                InstallStatus = $"Installation complete. {selected.Count} packages installed.";
            }
        }
        catch (OperationCanceledException)
        {
            InstallStatus = "Installation cancelled.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Installation failed");
            InstallStatus = $"Installation failed: {ex.Message}";
        }
        finally
        {
            CleanupSubscriptions();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            IsInstalling = false;
        }
    }

    [RelayCommand]
    private void CancelInstall()
    {
        _cancellationTokenSource?.Cancel();
        InstallStatus = "Cancelling...";
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
                "Disable Windows Defender Sample Submission" => @"reg add ""HKLM\SOFTWARE\Policies\Microsoft\Windows Defender\Spynet"" /v SubmitSamplesConsent /t REG_DWORD /d 0 /f",
                "Enable Hibernate" => @"powercfg /hibernate on",
                _ => ""
            };

            if (!string.IsNullOrEmpty(command))
            {
                var sanitizedCmd = SecurityUtilities.SanitizeArgument(command.Replace("reg ", ""));
                var exitCode = await _diagnostics.RunCommandAsync("reg", sanitizedCmd);
                InstallLog.Add($"[{DateTime.Now:HH:mm:ss}] {tweak.Name} - Exit code: {exitCode}");
            }
        }

        InstallStatus = "Registry tweaks applied.";
    }

    private void CleanupSubscriptions()
    {
        if (_outputHandler != null)
        {
            _diagnostics.OutputReceived -= _outputHandler;
            _outputHandler = null;
        }
        if (_errorHandler != null)
        {
            _diagnostics.ErrorReceived -= _errorHandler;
            _errorHandler = null;
        }
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