using ICNX.Scripting.Interfaces;
using ICNX.Scripting.Repositories;
using ICNX.Scripting.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ICNX.Scripting.Extensions;

/// <summary>
/// Extension methods for registering scripting services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add scripting services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddScriptingServices(this IServiceCollection services)
    {
        // Core scripting services
        services.AddSingleton<IUrlMatcher, UrlMatcher>();
        services.AddSingleton<IScriptEngine, BasicScriptEngine>();
        services.AddSingleton<IScriptRepository, InMemoryScriptRepository>();
        services.AddScoped<IContentExtractor, ContentExtractor>();

        // HTTP client for content extraction
        services.AddHttpClient<ContentExtractor>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        });

        return services;
    }

    /// <summary>
    /// Add scripting services with custom HTTP client configuration
    /// </summary>
    public static IServiceCollection AddScriptingServices(this IServiceCollection services, Action<HttpClient> configureHttpClient)
    {
        // Core scripting services
        services.AddSingleton<IUrlMatcher, UrlMatcher>();
        services.AddSingleton<IScriptEngine, BasicScriptEngine>();
        services.AddSingleton<IScriptRepository, InMemoryScriptRepository>();
        services.AddScoped<IContentExtractor, ContentExtractor>();

        // HTTP client with custom configuration
        services.AddHttpClient<ContentExtractor>(configureHttpClient);

        return services;
    }
}