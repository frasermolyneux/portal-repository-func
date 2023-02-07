using System.Reflection;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MX.GeoLocation.GeoLocationApi.Client;

using XtremeIdiots.Portal.RepositoryApiClient;
using XtremeIdiots.Portal.RepositoryFunc;
using XtremeIdiots.Portal.ServersApiClient;

var host = new HostBuilder()
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddUserSecrets(Assembly.GetExecutingAssembly(), true);
    })
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder
            .AddApplicationInsights()
            .AddApplicationInsightsLogger();
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        services.AddRepositoryApiClient(options =>
        {
            options.BaseUrl = config["apim_base_url"] ?? config["repository_base_url"] ?? throw new ApplicationException("Environment variable 'apim_base_url' has not been configured");
            options.ApiKey = config["portal_repository_apim_subscription_key"] ?? throw new ApplicationException("Environment variable 'portal_repository_apim_subscription_key' has not been configured");
            options.ApiAudience = config["repository_api_application_audience"] ?? throw new ApplicationException("Environment variable 'repository_api_application_audience' has not been configured");
            options.ApiPathPrefix = config["repository_api_path_prefix"] ?? "repository";
        });

        services.AddServersApiClient(options =>
        {
            options.BaseUrl = config["apim_base_url"] ?? config["servers_base_url"] ?? throw new ApplicationException("Environment variable 'apim_base_url' has not been configured");
            options.ApiKey = config["portal_servers_apim_subscription_key"] ?? throw new ApplicationException("Environment variable 'portal_servers_apim_subscription_key' has not been configured");
            options.ApiAudience = config["servers_api_application_audience"] ?? throw new ApplicationException("Environment variable 'servers_api_application_audience' has not been configured");
            options.ApiPathPrefix = config["servers_api_path_prefix"] ?? "servers";
        });

        services.AddGeoLocationApiClient(options =>
        {
            options.BaseUrl = config["apim_base_url"] ?? config["geolocation_base_url"] ?? throw new ApplicationException("Environment variable 'apim_base_url' has not been configured");
            options.ApiKey = config["geolocation_apim_subscription_key"] ?? throw new ApplicationException("Environment variable 'geolocation_apim_subscription_key' has not been configured");
            options.ApiAudience = config["geolocation_api_application_audience"] ?? throw new ApplicationException("Environment variable 'geolocation_api_application_audience' has not been configured");
            options.ApiPathPrefix = config["geolocation_api_path_prefix"] ?? "repository";
        });

        services.AddSingleton<ITelemetryInitializer, TelemetryInitializer>();
        services.AddLogging();
        services.AddMemoryCache();
    })
    .Build();

await host.RunAsync();