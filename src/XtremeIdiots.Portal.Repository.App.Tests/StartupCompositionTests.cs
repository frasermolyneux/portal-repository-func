using Microsoft.Extensions.DependencyInjection;

using MX.Api.Client.Extensions;

using XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Repository.App.Tests;

/// <summary>
/// Regression tests guarding the production Repository API client registration shape.
/// These validate that the exact <see cref="Program"/> registration composes without
/// throwing at runtime — the failure mode observed in PR #838 was an
/// <see cref="ArgumentException"/> thrown while constructing <see cref="IAdminActionsApi"/>
/// because a consumer-side caching policy invoked expressions declared outside the
/// target subclient interface.
/// </summary>
public sealed class StartupCompositionTests
{
    private const string BaseUrl = "https://repository.example.com";
    private const string Audience = "api://repository.example";

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

        return services.BuildServiceProvider(validateScopes: true);
    }
}
