using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using BhavaniSupportSuite.Core;
using BhavaniSupportSuite.Services;
using BhavaniSupportSuite.ViewModels;

namespace BhavaniSupportSuite;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;
    public static IHost AppHost { get; private set; } = null!;
    public static AppSettings Settings { get; private set; } = null!;
    public static bool IsRunningAsAdmin { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check for admin privileges
        IsRunningAsAdmin = IsRunningAsAdministrator();
        if (!IsRunningAsAdmin)
        {
            var result = MessageBox.Show(
                "Bhavani Support Suite Pro requires Administrator privileges for full functionality.\n\n" +
                "Some features (USB toggle, BitLocker, service management, deep clean, driver backup) " +
                "will not work without elevation.\n\n" +
                "Do you want to continue anyway?",
                "Administrator Privileges Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.No)
            {
                Shutdown();
                return;
            }
        }

        // Load settings early
        Settings = SettingsService.Load();

        // Configure Serilog
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BhavaniSupportSuite",
            "logs",
            "bhavani-support-suite-.log");

        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "BhavaniSupportSuitePro")
            .Enrich.WithProperty("Version", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0")
            .Enrich.WithProperty("IsAdmin", IsRunningAsAdmin)
            .WriteTo.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
            .CreateLogger();

        Log.Information("Starting Bhavani Support Suite Pro (Admin: {IsAdmin})", IsRunningAsAdmin);

        // Build DI container
        var builder = Host.CreateApplicationBuilder();
        
        builder.Services.AddBhavaniServices();
        
        AppHost = builder.Build();
        ServiceProvider = AppHost.Services;

        // Global exception handlers
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Log.Fatal(ex, "Fatal unhandled exception");
        };

        Log.Information("Application initialized successfully");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Save settings on exit
        try
        {
            if (ServiceProvider?.GetService(typeof(MainViewModel)) is MainViewModel mainVm)
            {
                Settings.IsSidebarExpanded = mainVm.IsSidebarExpanded;
            }
            SettingsService.Save(Settings);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings on exit");
        }

        Log.Information("Application shutting down");
        Log.CloseAndFlush();
        
        AppHost?.Dispose();
        base.OnExit(e);
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}