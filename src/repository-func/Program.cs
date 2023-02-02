using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MX.GeoLocation.GeoLocationApi.Client;

using XtremeIdiots.Portal.RepositoryApiClient;
using XtremeIdiots.Portal.RepositoryFunc;
using XtremeIdiots.Portal.ServersApiClient;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder
            .AddApplicationInsights()
            .AddApplicationInsightsLogger();
    })
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        services.AddRepositoryApiClient(options => new RepositoryApiClientOptions(
            config["apim_base_url"] ?? config["repository_base_url"] ?? throw new ArgumentNullException("apim_base_url"),
            config["portal_repository_apim_subscription_key"] ?? throw new ArgumentNullException("portal_repository_apim_subscription_key"),
            config["repository_api_application_audience"] ?? throw new ArgumentNullException("repository_api_application_audience"),
            config["repository_api_path_prefix"] ?? "repository")
        );

        services.AddServersApiClient(options => new ServersApiClientOptions(
            config["apim_base_url"] ?? config["servers_base_url"] ?? throw new ArgumentNullException("apim_base_url"),
            config["portal_servers_apim_subscription_key"] ?? throw new ArgumentNullException("portal_servers_apim_subscription_key"),
            config["servers_api_application_audience"] ?? throw new ArgumentNullException("servers_api_application_audience"),
            config["servers_api_path_prefix"] ?? "servers")
        );

        services.AddGeoLocationApiClient(options =>
        {
            options.BaseUrl = config["apim_base_url"] ?? config["geolocation_base_url"];
            options.ApiKey = config["geolocation_apim_subscription_key"];
        });

        services.AddSingleton<ITelemetryInitializer, TelemetryInitializer>();
        services.AddLogging();
        services.AddMemoryCache();
    })
    .Build();

await host.RunAsync();