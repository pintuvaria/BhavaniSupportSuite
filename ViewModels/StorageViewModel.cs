using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;

namespace BhavaniSupportSuite.ViewModels;

public partial class StorageViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<CleanResult> _cleanResults = new();

    [ObservableProperty]
    private ObservableCollection<DriverInfo> _drivers = new();

    [ObservableProperty]
    private ObservableCollection<string> _diskSpaceInfo = new();

    [ObservableProperty]
    private ObservableCollection<string> _cleanLog = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private string _backupDestination = string.Empty;

    public StorageViewModel()
    {
        Title = "Storage & Drivers";
        StatusMessage = "Ready";
        BackupDestination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DriverBackup");
        _ = LoadDiskInfoAsync();
    }

    private async Task LoadDiskInfoAsync()
    {
        await Task.Run(() =>
        {
            var info = StorageService.GetDiskSpaceInfo();
            SafeDispatch(() => DiskSpaceInfo = new ObservableCollection<string>(info));
        });
    }

    [RelayCommand]
    private async Task RunDeepCleanAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ProgressValue = 0;
        CleanResults.Clear();
        CleanLog.Clear();
        StatusMessage = "Running deep clean...";

        var progress = new Progress<string>(msg =>
        {
            SafeDispatch(() => CleanLog.Add(msg));
        });

        var results = await StorageService.RunDeepCleanAsync(progress);
        CleanResults = new ObservableCollection<CleanResult>(results);

        var totalFreed = results.Sum(r => r.BytesFreed) / (1024.0 * 1024);
        var totalFiles = results.Sum(r => r.FilesDeleted);
        StatusMessage = $"Clean complete. {totalFiles} files deleted, {totalFreed:F2} MB freed.";

        await LoadDiskInfoAsync();
        IsBusy = false;
    }

    [RelayCommand]
    private async Task LoadDriversAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading driver information...";

        var driverList = await StorageService.GetActiveDriversAsync();
        Drivers = new ObservableCollection<DriverInfo>(driverList);

        StatusMessage = $"Found {Drivers.Count} drivers.";
        IsBusy = false;
    }

    [RelayCommand]
    private async Task BackupDriversAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        StatusMessage = "Backing up drivers...";

        var success = await StorageService.BackupDriversAsync(BackupDestination);
        StatusMessage = success ? $"Drivers backed up to: {BackupDestination}" : "Driver backup failed.";

        IsBusy = false;
    }

    [RelayCommand]
    private async Task RefreshDiskInfoAsync()
    {
        await LoadDiskInfoAsync();
    }
}
