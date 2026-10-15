using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;
using Serilog;

namespace BhavaniSupportSuite.ViewModels;

public partial class PatchViewModel : ViewModelBase
{
    private readonly PatchService _patchService;

    [ObservableProperty]
    private ObservableCollection<WindowsUpdateInfo> _pendingUpdates = new();

    [ObservableProperty]
    private ObservableCollection<string> _updateStatus = new();

    [ObservableProperty]
    private ObservableCollection<string> _updateLog = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _progressValue;

    public PatchViewModel(PatchService patchService)
    {
        _patchService = patchService;
        Title = "Patch Management";
        StatusMessage = "Ready";
        _ = LoadStatusAsync();
    }

    private async Task LoadStatusAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading update status...";
        
        try
        {
            var status = await _patchService.GetUpdateStatusAsync();
            foreach (var s in status)
                UpdateStatus.Add(s);
            
            var pending = await _patchService.GetPendingUpdatesAsync();
            foreach (var u in pending)
                PendingUpdates.Add(u);
            
            StatusMessage = $"Found {PendingUpdates.Count} pending updates";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load update status");
            StatusMessage = "Failed to load update status";
        }
        
        IsBusy = false;
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsBusy) return;
        
        IsBusy = true;
        StatusMessage = "Checking for updates...";
        UpdateLog.Clear();
        
        try
        {
            var pending = await _patchService.GetPendingUpdatesAsync();
            PendingUpdates.Clear();
            foreach (var u in pending)
                PendingUpdates.Add(u);
            
            StatusMessage = $"Found {PendingUpdates.Count} pending updates";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check for updates");
            StatusMessage = $"Error: {ex.Message}";
        }
        
        IsBusy = false;
    }

    [RelayCommand]
    private async Task InstallUpdatesAsync()
    {
        if (IsBusy) return;
        
        IsBusy = true;
        ProgressValue = 0;
        UpdateLog.Clear();
        StatusMessage = "Installing updates...";
        
        try
        {
            var progress = new Progress<string>(msg =>
            {
                SafeDispatch(() => UpdateLog.Add($"[{DateTime.Now:HH:mm:ss}] {msg}"));
            });
            
            var exitCode = await _patchService.InstallUpdatesAsync(progress);
            
            UpdateLog.Add($"[{DateTime.Now:HH:mm:ss}] Installation completed with exit code: {exitCode}");
            StatusMessage = exitCode == 0 ? "Updates installed successfully" : "Installation failed";
            
            await LoadStatusAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to install updates");
            UpdateLog.Add($"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");
            StatusMessage = $"Installation failed: {ex.Message}";
        }
        
        IsBusy = false;
    }

    [RelayCommand]
    private async Task ViewHistoryAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading update history...";
        
        try
        {
            var history = await _patchService.GetWindowsUpdateHistoryAsync();
            UpdateLog.Add($"[{DateTime.Now:HH:mm:ss}] History loaded: {history.Count} entries");
            StatusMessage = "History loaded";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load history");
            StatusMessage = $"Error: {ex.Message}";
        }
        
        IsBusy = false;
    }
}