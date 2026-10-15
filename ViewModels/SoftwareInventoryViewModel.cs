using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;
using Serilog;

namespace BhavaniSupportSuite.ViewModels;

public partial class SoftwareInventoryViewModel : ViewModelBase
{
    private readonly SoftwareInventoryService _softwareService;

    [ObservableProperty]
    private ObservableCollection<InstalledSoftware> _installedSoftware = new();

    [ObservableProperty]
    private ObservableCollection<InstalledSoftware> _filteredSoftware = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private string _selectedSoftware = string.Empty;

    public SoftwareInventoryViewModel(SoftwareInventoryService softwareService)
    {
        _softwareService = softwareService;
        Title = "Software Inventory";
        StatusMessage = "Ready";
        _ = LoadSoftwareAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilterSoftware();
    }

    private async Task LoadSoftwareAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading software inventory...";
        
        try
        {
            var software = await _softwareService.GetInstalledSoftwareAsync();
            InstalledSoftware.Clear();
            foreach (var app in software)
                InstalledSoftware.Add(app);
            
            FilterSoftware();
            TotalCount = InstalledSoftware.Count;
            StatusMessage = $"Loaded {TotalCount} applications";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load software inventory");
            StatusMessage = $"Error: {ex.Message}";
        }
        
        IsBusy = false;
    }

    private void FilterSoftware()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            FilteredSoftware.Clear();
            foreach (var app in InstalledSoftware)
                FilteredSoftware.Add(app);
        }
        else
        {
            var filtered = InstalledSoftware.Where(x => 
                x.Name.ToLower().Contains(SearchQuery.ToLower()) || 
                x.Publisher.ToLower().Contains(SearchQuery.ToLower()) ||
                x.Version.Contains(SearchQuery)).ToList();
            
            FilteredSoftware.Clear();
            foreach (var app in filtered)
                FilteredSoftware.Add(app);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadSoftwareAsync();
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        if (string.IsNullOrEmpty(SelectedSoftware)) return;
        
        var software = FilteredSoftware.FirstOrDefault(x => x.Name == SelectedSoftware);
        if (software == null || string.IsNullOrEmpty(software.UninstallString)) return;

        try
        {
            var success = await _softwareService.UninstallSoftwareAsync(software.UninstallString);
            if (success)
            {
                InstalledSoftware.Remove(software);
                FilterSoftware();
                TotalCount = InstalledSoftware.Count;
                StatusMessage = $"{software.Name} uninstalled successfully";
            }
            else
            {
                StatusMessage = "Uninstall failed";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Uninstall failed");
            StatusMessage = $"Uninstall failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                $"SoftwareInventory_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            
            var lines = new List<string> { "Name,Version,Publisher,InstallDate,InstallLocation,Architecture,SizeKB,Source" };
            foreach (var app in InstalledSoftware)
            {
                lines.Add($"{EscapeCsv(app.Name)},{EscapeCsv(app.Version)},{EscapeCsv(app.Publisher)},{EscapeCsv(app.InstallDate)},{EscapeCsv(app.InstallLocation)},{EscapeCsv(app.Architecture)},{app.EstimatedSize / 1024},{EscapeCsv(app.Source)}");
            }
            
            await File.WriteAllLinesAsync(path, lines);
            StatusMessage = $"Exported to {path}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Export failed");
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}