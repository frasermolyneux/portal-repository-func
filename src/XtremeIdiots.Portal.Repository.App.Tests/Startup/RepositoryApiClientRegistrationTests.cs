using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using MX.Api.Client.Extensions;

using XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Repository.App.Tests.Startup;

/// <summary>
/// Regression tests that guard the Repository API client registration performed by
/// <c>Program.cs</c>. PR #838 introduced <c>.WithCaching(c =&gt; c.UseLibraryDefaults())</c>
/// against Repository Api.Client 4.2.21, which shares the caching configure action across
/// every typed subclient and evaluates cache expressions during startup. That produced
/// <see cref="ArgumentException"/> messages like:
///     "The expression must invoke a method declared by
///      XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1.IAdminActionsApi
///      or one of its inherited interfaces."
///
/// The tests below mirror the production registration and force DI resolution of the
/// aggregate client and the representative subclients that are used by this Function App.
/// If a future change re-adds an incompatible consumer-side cache configuration, those
/// registrations will throw during construction and these tests will fail fast.
/// </summary>
public sealed class RepositoryApiClientRegistrationTests
{
    private const string BaseUrl = "https://repository-api.example.invalid";
    private const string Audience = "api://portal-repository-tests";

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RepositoryApi:BaseUrl"] = BaseUrl,
                ["RepositoryApi:ApplicationAudience"] = Audience,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Mirrors src/XtremeIdiots.Portal.Repository.App/Program.cs registration exactly.
        services.AddRepositoryApiClient(options => options
            .WithBaseUrl(configuration["RepositoryApi:BaseUrl"]!)
            .WithEntraIdAuthentication(configuration["RepositoryApi:ApplicationAudience"]!));

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void AddRepositoryApiClient_ResolvesAggregateClientWithoutThrowing()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IRepositoryApiClient>();

        Assert.NotNull(client);
    }

    /// <summary>
    /// Force resolution of every typed subclient the Function App consumes at runtime.
    /// The PR #838 crash surfaced when the shared caching configure action was applied to
    /// each subclient - resolving them here guarantees we execute the same code path.
    /// </summary>
    [Fact]
    public void AddRepositoryApiClient_ResolvesTypedSubclientsUsedByThisApp()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IRepositoryApiClient>();

        // Exercised by UnclaimedActionReminder - this is the exact subclient named in
        // the production ArgumentException message.
        Assert.NotNull(client.AdminActions);
        Assert.NotNull(client.AdminActions.V1);

        Assert.NotNull(client.UserProfiles);
        Assert.NotNull(client.UserProfiles.V1);

        Assert.NotNull(client.Notifications);
        Assert.NotNull(client.Notifications.V1);

        // Exercised by MapPopularity.
        Assert.NotNull(client.Maps);
        Assert.NotNull(client.Maps.V1);

        // Exercised by DataMaintenance timers.
        Assert.NotNull(client.DataMaintenance);
        Assert.NotNull(client.DataMaintenance.V1);

        // Exercised by VpnDetectedTagReconciler.
        Assert.NotNull(client.Players);
        Assert.NotNull(client.Players.V1);
    }

    [Fact]
    public void AddRepositoryApiClient_RepresentativeSubclient_ResolvesAsIAdminActionsApi()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IRepositoryApiClient>();

        var adminActionsApi = client.AdminActions.V1;

        Assert.NotNull(adminActionsApi);
        Assert.IsAssignableFrom<IAdminActionsApi>(adminActionsApi);
    }

    /// <summary>
    /// Explicit witness that documents the PR #838 production crash: calling
    /// <c>.WithCaching(c =&gt; c.UseLibraryDefaults())</c> against Repository Api.Client
    /// 4.2.21 throws <see cref="ArgumentException"/> during startup - the failure surfaces
    /// as soon as the typed client options are validated (or, in the observed production
    /// stack, when the shared cache configure action is applied to a typed subclient such
    /// as <see cref="IAdminActionsApi"/>).
    ///
    /// The purpose of this test is:
    /// 1. Pin the exact failure mode that motivated the hotfix so it is not silently
    ///    reintroduced.
    /// 2. Fail loudly (turning green in an unexpected way) if a future MX.Api.Client /
    ///    Repository Api.Client release fixes the consumer-side caching defect - at
    ///    that point we can safely re-enable client-side caching in Program.cs.
    /// </summary>
    [Fact]
    public void AddRepositoryApiClient_WithClientSideCaching_IsKnownIncompatibleWith_4_2_21()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var ex = Record.Exception(() =>
        {
            services.AddRepositoryApiClient(options => options
                .WithBaseUrl(BaseUrl)
                .WithEntraIdAuthentication(Audience)
                .WithCaching(c => c.UseLibraryDefaults()));

            using var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IRepositoryApiClient>();
            _ = client.AdminActions.V1;
        });

        Assert.NotNull(ex);
        Assert.IsAssignableFrom<ArgumentException>(ex);
    }
}
