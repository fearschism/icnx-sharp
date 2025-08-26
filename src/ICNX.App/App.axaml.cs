using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using ICNX.App.ViewModels;
using ICNX.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ICNX.Core.Interfaces;
using ICNX.Core.Services;
using ICNX.Download;
using ICNX.Persistence.Repositories;
using ICNX.Persistence.Migrations;
using ICNX.App.Services;
using ICNX.Core.Models;
using System.IO;
using System;

namespace ICNX.App;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ConfigureServices();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            // Create MainWindow with DI
            var mainWindowViewModel = _serviceProvider!.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel,
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder => builder.AddConsole());

        // Core services
        services.AddSingleton<IEventAggregator, EventAggregator>();
        services.AddSingleton<IProgressTracker, ProgressTracker>();
        services.AddSingleton<UIProgressService>();
        services.AddSingleton<ToastNotificationService>();
        services.AddSingleton<ProgressAggregationService>();

        // Database - for now using in-memory connection string
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ICNX", "downloads.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var connectionString = $"Data Source={dbPath}";

        services.AddSingleton<IRepository<DownloadSession>>(provider =>
            new DownloadSessionRepository(connectionString, provider.GetRequiredService<ILogger<DownloadSessionRepository>>()));
        services.AddSingleton<IRepository<DownloadItem>>(provider =>
            new DownloadItemRepository(connectionString, provider.GetRequiredService<ILogger<DownloadItemRepository>>()));

        // Settings repository and service
        services.AddSingleton<ISettingsRepository>(provider =>
            new SettingsRepository(connectionString, provider.GetRequiredService<ILogger<SettingsRepository>>()));
        services.AddSingleton<ISettingsService, ICNX.Persistence.Services.SettingsService>();

        // Download services
        services.AddSingleton<IDownloadEngine, DownloadEngine>();
        services.AddSingleton<IDownloadSessionService, DownloadSessionService>();

        // Migration runner
        services.AddSingleton(provider =>
            new MigrationRunner(connectionString, provider.GetRequiredService<ILogger<MigrationRunner>>()));

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsViewModel>();

        _serviceProvider = services.BuildServiceProvider();

        // Run database migrations on startup
        var migrationRunner = _serviceProvider.GetRequiredService<MigrationRunner>();
        migrationRunner.RunMigrationsAsync().GetAwaiter().GetResult();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}