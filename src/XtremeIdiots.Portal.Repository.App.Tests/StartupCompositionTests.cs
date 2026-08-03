using Microsoft.Extensions.DependencyInjection;

using MX.Api.Client.Extensions;
using MX.GeoLocation.Api.Client.V1;

using XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Repository.App.Tests;

/// <summary>
/// Regression tests guarding the production Repository API + GeoLocation API client
/// registration shape. These validate that the exact <see cref="Program"/> registration
/// composes without throwing at runtime — the failure mode observed in PR #838 was an
/// <see cref="ArgumentException"/> thrown while constructing typed sub-APIs because a
/// consumer-side caching policy invoked expressions declared outside the target
/// subclient interface. From MX.Api.Client 2.3.77 onwards this is handled reflection-
/// free via SharedCacheConfiguration, and the GeoLocation client 1.2.98 ships curated
/// read-only cache defaults consumed via <c>UseLibraryDefaults()</c>.
/// </summary>
public sealed class StartupCompositionTests
{
    private const string BaseUrl = "https://repository.example.com";
    private const string Audience = "api://repository.example";
    private const string GeoBaseUrl = "https://geolocation.example.com";
    private const string GeoAudience = "api://geolocation.example";
    private const string GeoApiKey = "test-api-key";

    [Fact]
    public void AddRepositoryApiClient_WithProductionShape_ResolvesRepositoryClient()
    {
        using var provider = BuildProductionProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IRepositoryApiClient>();

        Assert.NotNull(client);
    }

    [Fact]
    public void AddRepositoryApiClient_WithProductionShape_ResolvesAdminActionsSubclient()
    {
        using var provider = BuildProductionProvider();
        using var scope = provider.CreateScope();

        var adminActions = scope.ServiceProvider.GetRequiredService<IAdminActionsApi>();

        Assert.NotNull(adminActions);
    }

    [Theory]
    [MemberData(nameof(RepresentativeSubclients))]
    public void AddRepositoryApiClient_WithProductionShape_ResolvesRepresentativeSubclients(Type subclientType)
    {
        using var provider = BuildProductionProvider();
        using var scope = provider.CreateScope();

        var subclient = scope.ServiceProvider.GetRequiredService(subclientType);

        Assert.NotNull(subclient);
    }

    [Fact]
    public void AddGeoLocationApiClient_WithProductionShape_ResolvesGeoLocationClient()
    {
        using var provider = BuildProductionProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IGeoLocationApiClient>();

        Assert.NotNull(client);
    }

    [Theory]
    [MemberData(nameof(GeoLocationSubclients))]
    public void AddGeoLocationApiClient_WithProductionShape_ResolvesTypedSubclients(Type subclientType)
    {
        using var provider = BuildProductionProvider();
        using var scope = provider.CreateScope();

        var subclient = scope.ServiceProvider.GetRequiredService(subclientType);

        Assert.NotNull(subclient);
    }

    public static TheoryData<Type> RepresentativeSubclients()
    {
        return
        [
            typeof(IAdminActionsApi),
            typeof(IPlayersApi),
            typeof(IGameServersApi),
            typeof(IChatMessagesApi),
            typeof(IMapsApi),
            typeof(IDataMaintenanceApi),
            typeof(IUserProfileApi),
            typeof(INotificationsApi),
        ];
    }

    public static TheoryData<Type> GeoLocationSubclients()
    {
        return
        [
            typeof(MX.GeoLocation.Abstractions.Interfaces.V1.IGeoLookupApi),
            typeof(MX.GeoLocation.Abstractions.Interfaces.V1_1.IGeoLookupApi),
        ];
    }

    private static ServiceProvider BuildProductionProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Must mirror Program.cs exactly, including .WithCaching(...) — this is the registration
        // shape that crashed in production with Repository client 4.2.21 / MX.Api.Client 2.3.76
        // (ArgumentException fanning a single cache delegate across every typed sub-API).
        services.AddRepositoryApiClient(options => options
            .WithBaseUrl(BaseUrl)
            .WithEntraIdAuthentication(Audience)
            .WithCachePartition("portal-repository-func")
            .WithCaching(c => c.UseLibraryDefaults()));

        // Mirrors the GeoLocation registration in Program.cs, including read-only cache
        // defaults from GeoLocation client 1.2.98. This guards against a recurrence of the
        // same DI-composition failure mode on GeoLocation typed sub-APIs (v1 + v1.1).
        services.AddGeoLocationApiClient(options => options
            .WithBaseUrl(GeoBaseUrl)
            .WithApiKeyAuthentication(GeoApiKey, "Ocp-Apim-Subscription-Key")
            .WithEntraIdAuthentication(GeoAudience)
            .WithCachePartition("portal-repository-func")
            .WithCaching(c => c.UseLibraryDefaults()));

        return services.BuildServiceProvider(validateScopes: true);
    }
}
