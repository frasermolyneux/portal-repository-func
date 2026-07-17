using System.Net;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Moq;

using MX.Api.Abstractions;
using MX.GeoLocation.Abstractions.Models.V1_1;
using MX.GeoLocation.Api.Client.V1;

using Newtonsoft.Json;

using XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Players;
using XtremeIdiots.Portal.Repository.Api.Client.V1;
using XtremeIdiots.Portal.Repository.App.Services;

namespace XtremeIdiots.Portal.Repository.App.Tests.Services;

public sealed class VpnDetectedTagReconcilerTests
{
    private static readonly Guid PlayerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PlayerIpAddressId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IRepositoryApiClient> repositoryApiClient = new();
    private readonly Mock<IVersionedPlayersApi> versionedPlayersApi = new();
    private readonly Mock<IPlayersApi> playersApi = new();
    private readonly Mock<IGeoLocationApiClient> geoLocationApiClient = new();
    private readonly Mock<IVersionedGeoLookupApi> versionedGeoLookupApi = new();
    private readonly Mock<MX.GeoLocation.Abstractions.Interfaces.V1_1.IGeoLookupApi> geoLookupApi = new();
    private readonly Mock<ILogger<VpnDetectedTagReconciler>> logger = new();
    private readonly Dictionary<string, bool> vpnByAddress = new(StringComparer.OrdinalIgnoreCase);

    public VpnDetectedTagReconcilerTests()
    {
        versionedPlayersApi.Setup(x => x.V1).Returns(playersApi.Object);
        repositoryApiClient.Setup(x => x.Players).Returns(versionedPlayersApi.Object);
        versionedGeoLookupApi.Setup(x => x.V1_1).Returns(geoLookupApi.Object);
        geoLocationApiClient.Setup(x => x.GeoLookup).Returns(versionedGeoLookupApi.Object);
        geoLookupApi
            .Setup(x => x.GetIpIntelligences(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> addresses, CancellationToken _) =>
                new ApiResult<CollectionModel<IpIntelligenceDto>>(
                    HttpStatusCode.OK,
                    new ApiResponse<CollectionModel<IpIntelligenceDto>>(
                        new CollectionModel<IpIntelligenceDto>(
                            [.. addresses.Select(address => CreateIntelligence(address, vpnByAddress.GetValueOrDefault(address)))]))));
        playersApi
            .Setup(x => x.SetVpnDetectedTag(It.IsAny<Guid>(), It.IsAny<SetVpnDetectedTagDto>()))
            .ReturnsAsync(new ApiResult(HttpStatusCode.OK));
    }

    [Fact]
    public async Task ReconcileAsync_VpnAddress_AddsTag()
    {
        var candidate = CreateCandidate(hasVpnDetectedTag: false);
        SetupCandidates(CreatePage([candidate]));
        SetupIntelligence("198.51.100.10", isVpn: true);

        var summary = await CreateSut(enabled: true).ReconcileAsync(force: false);

        Assert.Equal(1, summary.TagsAdded);
        playersApi.Verify(
            x => x.SetVpnDetectedTag(PlayerId, It.Is<SetVpnDetectedTagDto>(dto => dto.IsDetected)),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_CleanAddress_RemovesExistingTag()
    {
        var candidate = CreateCandidate(hasVpnDetectedTag: true);
        SetupCandidates(CreatePage([candidate]));
        SetupIntelligence("198.51.100.10", isVpn: false);

        var summary = await CreateSut(enabled: true).ReconcileAsync(force: false);

        Assert.Equal(1, summary.TagsRemoved);
        playersApi.Verify(
            x => x.SetVpnDetectedTag(PlayerId, It.Is<SetVpnDetectedTagDto>(dto => !dto.IsDetected)),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_IntelligenceFailure_RetainsExistingTag()
    {
        var candidate = CreateCandidate(hasVpnDetectedTag: true);
        SetupCandidates(CreatePage([candidate]));
        geoLookupApi
            .Setup(x => x.GetIpIntelligences(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<CollectionModel<IpIntelligenceDto>>(HttpStatusCode.ServiceUnavailable));

        var summary = await CreateSut(enabled: true).ReconcileAsync(force: false);

        Assert.Equal(1, summary.PlayersSkipped);
        playersApi.Verify(x => x.SetVpnDetectedTag(It.IsAny<Guid>(), It.IsAny<SetVpnDetectedTagDto>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_PartialIntelligence_RetainsExistingTag()
    {
        var candidate = CreateCandidate(hasVpnDetectedTag: true);
        SetupCandidates(CreatePage([candidate]));
        geoLookupApi
            .Setup(x => x.GetIpIntelligences(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<CollectionModel<IpIntelligenceDto>>(
                HttpStatusCode.OK,
                new ApiResponse<CollectionModel<IpIntelligenceDto>>(
                    new CollectionModel<IpIntelligenceDto>([CreatePartialIntelligence(candidate.IpAddress)]))));

        var summary = await CreateSut(enabled: true).ReconcileAsync(force: false);

        Assert.Equal(1, summary.PlayersSkipped);
        playersApi.Verify(x => x.SetVpnDetectedTag(It.IsAny<Guid>(), It.IsAny<SetVpnDetectedTagDto>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_AnyRecentVpnAddress_AddsTag()
    {
        var cleanCandidate = CreateCandidate(ipAddress: "198.51.100.10", hasVpnDetectedTag: false);
        var vpnCandidate = CreateCandidate(
            playerIpAddressId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ipAddress: "198.51.100.11",
            hasVpnDetectedTag: false);
        SetupCandidates(CreatePage([cleanCandidate, vpnCandidate]));
        SetupIntelligence(cleanCandidate.IpAddress, isVpn: false);
        SetupIntelligence(vpnCandidate.IpAddress, isVpn: true);

        var summary = await CreateSut(enabled: true).ReconcileAsync(force: false);

        Assert.Equal(1, summary.TagsAdded);
        playersApi.Verify(
            x => x.SetVpnDetectedTag(PlayerId, It.Is<SetVpnDetectedTagDto>(dto => dto.IsDetected)),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_Disabled_DoesNotCallExternalServices()
    {
        await CreateSut(enabled: false).ReconcileAsync(force: false);

        playersApi.Verify(
            x => x.GetVpnDetectedTagReconciliationCandidates(It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_CompositeCursor_ReadsAllPagesBeforeMutation()
    {
        var firstCandidate = CreateCandidate(playerIpAddressId: PlayerIpAddressId, hasVpnDetectedTag: false);
        var secondCandidate = CreateCandidate(
            playerId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            playerIpAddressId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            ipAddress: "198.51.100.11",
            hasVpnDetectedTag: false);
        var cursorTime = firstCandidate.LastUsed;
        SetupCandidates(
            CreatePage([firstCandidate], cursorTime, firstCandidate.PlayerIpAddressId),
            CreatePage([secondCandidate]));
        SetupIntelligence("198.51.100.10", isVpn: true);
        SetupIntelligence("198.51.100.11", isVpn: true);

        var summary = await CreateSut(enabled: true).ReconcileAsync(force: false);

        Assert.Equal(2, summary.TagsAdded);
        playersApi.Verify(
            x => x.GetVpnDetectedTagReconciliationCandidates(
                It.IsAny<DateTime>(),
                cursorTime,
                firstCandidate.PlayerIpAddressId,
                It.IsAny<int>()),
            Times.Once);
    }

    private VpnDetectedTagReconciler CreateSut(bool enabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VpnDetectedTagReconciliation:Enabled"] = enabled.ToString()
            })
            .Build();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(geoLocationApiClient.Object)
            .BuildServiceProvider();
        return new VpnDetectedTagReconciler(repositoryApiClient.Object, serviceProvider, configuration, logger.Object);
    }

    private void SetupCandidates(params VpnDetectedTagReconciliationPageDto[] pages)
    {
        var pageIndex = 0;
        playersApi
            .Setup(x => x.GetVpnDetectedTagReconciliationCandidates(It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<int>()))
            .ReturnsAsync(() => new ApiResult<VpnDetectedTagReconciliationPageDto>(
                HttpStatusCode.OK,
                new ApiResponse<VpnDetectedTagReconciliationPageDto>(pages[pageIndex++])));
    }

    private void SetupIntelligence(string address, bool isVpn)
    {
        vpnByAddress[address] = isVpn;
    }

    private static VpnDetectedTagReconciliationCandidateDto CreateCandidate(
        Guid? playerId = null,
        Guid? playerIpAddressId = null,
        string ipAddress = "198.51.100.10",
        bool hasVpnDetectedTag = false) =>
        JsonConvert.DeserializeObject<VpnDetectedTagReconciliationCandidateDto>(JsonConvert.SerializeObject(new
        {
            PlayerId = playerId ?? PlayerId,
            PlayerIpAddressId = playerIpAddressId ?? PlayerIpAddressId,
            IpAddress = ipAddress,
            LastUsed = DateTime.UtcNow.AddHours(-1),
            HasVpnDetectedTag = hasVpnDetectedTag
        }))!;

    private static VpnDetectedTagReconciliationPageDto CreatePage(
        IReadOnlyList<VpnDetectedTagReconciliationCandidateDto> candidates,
        DateTime? nextLastUsedUtc = null,
        Guid? nextPlayerIpAddressId = null) =>
        JsonConvert.DeserializeObject<VpnDetectedTagReconciliationPageDto>(JsonConvert.SerializeObject(new
        {
            Candidates = candidates,
            NextLastUsedUtc = nextLastUsedUtc,
            NextPlayerIpAddressId = nextPlayerIpAddressId
        }))!;

    private static IpIntelligenceDto CreateIntelligence(string address, bool isVpn) =>
        JsonConvert.DeserializeObject<IpIntelligenceDto>(JsonConvert.SerializeObject(new
        {
            Address = address,
            ProxyCheck = new
            {
                Address = address,
                IsVpn = isVpn
            }
        }))!;

    private static IpIntelligenceDto CreatePartialIntelligence(string address) =>
        JsonConvert.DeserializeObject<IpIntelligenceDto>(JsonConvert.SerializeObject(new { Address = address }))!;
}