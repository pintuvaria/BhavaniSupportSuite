using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;
using BhavaniSupportSuite.ViewModels;
using BhavaniSupportSuite.Views;

namespace BhavaniSupportSuite;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBhavaniServices(this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<DiagnosticsService>();
        services.AddSingleton<NetworkScanner>();
        services.AddSingleton<HardwareService>();
        services.AddSingleton<SecurityService>();
        services.AddSingleton<StorageService>();
        services.AddSingleton<ReportsService>();

        // ViewModels
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<HardwareViewModel>();
        services.AddTransient<StagingViewModel>();
        services.AddTransient<NetworkViewModel>();
        services.AddTransient<SecurityViewModel>();
        services.AddTransient<VaultViewModel>();
        services.AddTransient<StorageViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<RecoveryViewModel>();
        services.AddTransient<UtilitiesViewModel>();
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<DashboardView>();
        services.AddTransient<HardwareView>();
        services.AddTransient<StagingView>();
        services.AddTransient<NetworkView>();
        services.AddTransient<SecurityView>();
        services.AddTransient<VaultView>();
        services.AddTransient<StorageView>();
        services.AddTransient<ReportsView>();
        services.AddTransient<RecoveryView>();
        services.AddTransient<UtilitiesView>();

        return services;
    }
}

public class AppSettings
{
    public string LastUsedSubnet { get; set; } = "192.168.1.0/24";
    public string LastTargetIp { get; set; } = "192.168.1.1";
    public string LastPortRange { get; set; } = "22,80,135,445,3389,8080";
    public string BackupDestination { get; set; } = string.Empty;
    public string ReportPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    public bool IsSidebarExpanded { get; set; } = true;
    public double WindowWidth { get; set; } = 1400;
    public double WindowHeight { get; set; } = 800;
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public bool RememberWindowPosition { get; set; } = true;
    public int DeepCleanRetentionDays { get; set; } = 30;
    public bool AutoRefreshEnabled { get; set; } = true;
    public int RefreshIntervalSeconds { get; set; } = 2;
}

public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BhavaniSupportSuite",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(settings, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}