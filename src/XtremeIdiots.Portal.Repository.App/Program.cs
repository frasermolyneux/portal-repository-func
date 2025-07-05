using System.Reflection;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MX.Api.Client.Extensions;
using MX.GeoLocation.GeoLocationApi.Client;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;
using XtremeIdiots.Portal.Repository.App;

var host = new HostBuilder()
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddUserSecrets(Assembly.GetExecutingAssembly(), true);
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        services.AddLogging();
        services.AddSingleton<ITelemetryInitializer, TelemetryInitializer>();
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        services.AddRepositoryApiClient(options =>
        {
            options.BaseUrl = config["repository_base_url"] ?? config["apim_base_url"] ?? throw new ArgumentNullException("apim_base_url");
            options.PrimaryApiKey = config["portal_repository_apim_subscription_key_primary"] ?? throw new ArgumentNullException("portal_repository_apim_subscription_key_primary");
            options.SecondaryApiKey = config["portal_repository_apim_subscription_key_secondary"] ?? throw new ArgumentNullException("portal_repository_apim_subscription_key_secondary");
            options.ApiAudience = config["repository_api_application_audience"] ?? throw new ArgumentNullException("repository_api_application_audience");
            options.ApiPathPrefix = config["repository_api_path_prefix"] ?? "repository";
        });

        services.AddServersApiClient()
            .WithBaseUrl(config["ServersIntegrationApi:BaseUrl"] ?? throw new ArgumentNullException("ServersIntegrationApi:BaseUrl"))
            .WithApiKeyAuthentication(config["ServersIntegrationApi:ApiKey"] ?? throw new ArgumentNullException("ServersIntegrationApi:ApiKey"))
            .WithAzureCredentials(config["ServersIntegrationApi:ApplicationAudience"] ?? throw new ArgumentNullException("ServersIntegrationApi:ApplicationAudience"));

        services.AddGeoLocationApiClient(options =>
        {
            options.BaseUrl = config["geolocation_base_url"] ?? config["apim_base_url"] ?? throw new ArgumentNullException("apim_base_url");
            options.PrimaryApiKey = config["geolocation_apim_subscription_key_primary"] ?? throw new ArgumentNullException("geolocation_apim_subscription_key_primary");
            options.SecondaryApiKey = config["geolocation_apim_subscription_key_secondary"] ?? throw new ArgumentNullException("geolocation_apim_subscription_key_secondary");
            options.ApiAudience = config["geolocation_api_application_audience"] ?? throw new ArgumentNullException("geolocation_api_application_audience");
            options.ApiPathPrefix = config["geolocation_api_path_prefix"] ?? "geolocation";
        });

        services.AddMemoryCache();

        services.AddHealthChecks();
    })
    .Build();

await host.RunAsync();