using System.Reflection;

using Azure.Identity;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using MX.Api.Client.Extensions;
using MX.Api.Client.Configuration;
using MX.GeoLocation.Api.Client.V1;
using MX.Observability.ApplicationInsights.WorkerService;
using XtremeIdiots.Portal.Repository.Api.Client.V1;
using XtremeIdiots.Portal.Repository.App;
using XtremeIdiots.Portal.Repository.App.Services;

var host = new HostBuilder()
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddEnvironmentVariables();
        builder.AddUserSecrets(Assembly.GetExecutingAssembly(), true);

        var builtConfig = builder.Build();
        var appConfigEndpoint = builtConfig["AzureAppConfiguration:Endpoint"];

        if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
        {
            var managedIdentityClientId = builtConfig["AzureAppConfiguration:ManagedIdentityClientId"];
            var environmentLabel = builtConfig["AzureAppConfiguration:Environment"];

            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = managedIdentityClientId,
            });

            builder.AddAzureAppConfiguration(options =>
            {
                options.Connect(new Uri(appConfigEndpoint), credential)
                    .Select("RepositoryApi:*", environmentLabel)
                    .Select("GeoLocationApi:*", environmentLabel)
                    .Select("XtremeIdiots.Portal.Repository.App:*", environmentLabel)
                    .TrimKeyPrefix("XtremeIdiots.Portal.Repository.App:")
                    .Select("ApplicationInsights:*", environmentLabel)
                    .ConfigureRefresh(refresh =>
                    {
                        refresh.Register("Sentinel", environmentLabel, refreshAll: true)
                               .SetRefreshInterval(TimeSpan.FromMinutes(5));
                    });

                options.ConfigureKeyVault(kv =>
                {
                    kv.SetCredential(credential);
                    kv.SetSecretRefreshInterval(TimeSpan.FromHours(1));
                });
            });
        }
    })
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.Services.AddAzureAppConfiguration();
        builder.UseAzureAppConfiguration();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        ValidateTelemetryFilterConfiguration(configuration);

        services.AddLogging();
        services.AddSingleton<ITelemetryInitializer, TelemetryInitializer>();
        services.AddApplicationInsightsTelemetryWorkerService(options =>
        {
            options.EnableAdaptiveSampling = false;
        });
        services.ConfigureFunctionsApplicationInsights();
        services.AddObservability();

        services.AddRepositoryApiClient(options => options
            .WithBaseUrl(configuration["RepositoryApi:BaseUrl"] ?? throw new InvalidOperationException("RepositoryApi:BaseUrl configuration is required"))
            .WithEntraIdAuthentication(configuration["RepositoryApi:ApplicationAudience"] ?? throw new InvalidOperationException("RepositoryApi:ApplicationAudience configuration is required")));

        var geoBaseUrl = configuration["GeoLocationApi:BaseUrl"];
        var geoApiKey = configuration["GeoLocationApi:ApiKey"];
        var geoAudience = configuration["GeoLocationApi:ApplicationAudience"];
        if (!string.IsNullOrWhiteSpace(geoBaseUrl) &&
            !string.IsNullOrWhiteSpace(geoApiKey) &&
            !string.IsNullOrWhiteSpace(geoAudience))
        {
            services.AddGeoLocationApiClient(options => options
                .WithBaseUrl(geoBaseUrl)
                .WithApiKeyAuthentication(geoApiKey, "Ocp-Apim-Subscription-Key")
                .WithEntraIdAuthentication(geoAudience));
        }

        services.AddSingleton<IVpnDetectedTagReconciler, VpnDetectedTagReconciler>();

        services.AddHealthChecks();
    })
    .Build();

await host.RunAsync().ConfigureAwait(false);

static void ValidateTelemetryFilterConfiguration(IConfiguration configuration)
{
    if (string.IsNullOrWhiteSpace(configuration["AzureAppConfiguration:Endpoint"]))
    {
        return;
    }

    var requiredKeys = new[]
    {
        "ApplicationInsights:TelemetryFilter:Enabled",
        "ApplicationInsights:TelemetryFilter:Requests:Enabled",
        "ApplicationInsights:TelemetryFilter:Traces:Enabled",
        "ApplicationInsights:TelemetryFilter:CustomEvents:Enabled",
    };

    var missingKeys = requiredKeys
        .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
        .ToList();

    var hasAllowedNamePrefixes =
        !string.IsNullOrWhiteSpace(configuration["ApplicationInsights:TelemetryFilter:CustomEvents:AllowedNamePrefixes:0"]) ||
        !string.IsNullOrWhiteSpace(configuration["ApplicationInsights:TelemetryFilter:CustomEvents:AllowedNamePrefixes"]);

    if (!hasAllowedNamePrefixes)
    {
        missingKeys.Add("ApplicationInsights:TelemetryFilter:CustomEvents:AllowedNamePrefixes");
    }

    if (missingKeys.Count > 0)
    {
        throw new InvalidOperationException(
            $"Missing required App Configuration telemetry filter keys: {string.Join(", ", missingKeys)}");
    }
}
