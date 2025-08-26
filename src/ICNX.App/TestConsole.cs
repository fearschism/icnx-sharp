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

public class TestConsole
{
    public static void TestServiceContainer()
    {
        try
        {
            Console.WriteLine("Testing service container initialization...");

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

            var serviceProvider = services.BuildServiceProvider();

            Console.WriteLine("✅ Service container initialized successfully!");

            // Test getting a service
            var downloadService = serviceProvider.GetRequiredService<IDownloadSessionService>();
            Console.WriteLine("✅ DownloadSessionService resolved successfully!");

            // Test settings service
            var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
            Console.WriteLine("✅ SettingsService resolved successfully!");

            // Run migrations
            Console.WriteLine("Running database migrations...");
            var migrationRunner = serviceProvider.GetRequiredService<MigrationRunner>();
            migrationRunner.RunMigrationsAsync().GetAwaiter().GetResult();
            Console.WriteLine("✅ Database migrations completed successfully!");

            Console.WriteLine("\n🎉 All services are working correctly!");
            Console.WriteLine("The app infrastructure is ready and would display a window in a desktop environment.");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
