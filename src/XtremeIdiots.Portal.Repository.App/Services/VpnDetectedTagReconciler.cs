using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using MX.Api.Abstractions;
using MX.GeoLocation.Abstractions.Models.V1_1;
using MX.GeoLocation.Api.Client.V1;

using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Players;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Repository.App.Services;

public sealed class VpnDetectedTagReconciler(
    IRepositoryApiClient repositoryApiClient,
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<VpnDetectedTagReconciler> logger) : IVpnDetectedTagReconciler
{
    private const int MaxGeoLocationBatchSize = 20;
    private const int DefaultPageSize = 200;
    private const int MaximumPageSize = 500;

    public async Task<VpnDetectedTagReconciliationSummary> ReconcileAsync(bool force, CancellationToken cancellationToken = default)
    {
        if (!force && !configuration.GetValue("VpnDetectedTagReconciliation:Enabled", false))
        {
            logger.LogInformation("VPN detected tag reconciliation is disabled");
            return new VpnDetectedTagReconciliationSummary(0, 0, 0, 0, 0);
        }

        var geoLocationApiClient = serviceProvider.GetService<IGeoLocationApiClient>();
        if (geoLocationApiClient is null)
        {
            logger.LogWarning("Skipping VPN detected tag reconciliation because GeoLocationApi is not configured");
            return new VpnDetectedTagReconciliationSummary(0, 0, 0, 0, 0);
        }

        var cutoffUtc = DateTime.UtcNow.AddDays(-30);
        var pageSize = Math.Clamp(configuration.GetValue<int?>("VpnDetectedTagReconciliation:PageSize") ?? DefaultPageSize, 1, MaximumPageSize);
        var candidates = await GetCandidatesAsync(cutoffUtc, pageSize, cancellationToken).ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            return new VpnDetectedTagReconciliationSummary(0, 0, 0, 0, 0);
        }

        var intelligenceByAddress = await GetIntelligenceAsync(geoLocationApiClient, candidates, cancellationToken).ConfigureAwait(false);
        var tagsAdded = 0;
        var tagsRemoved = 0;
        var playersSkipped = 0;

        foreach (var playerCandidates in candidates.GroupBy(candidate => candidate.PlayerId))
        {
            var addresses = playerCandidates
                .Select(candidate => candidate.IpAddress)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (addresses.Any(address => !intelligenceByAddress.ContainsKey(address)))
            {
                playersSkipped++;
                continue;
            }

            var isDetected = addresses.Any(address => intelligenceByAddress[address].ProxyCheck?.IsVpn == true);
            var hasVpnDetectedTag = playerCandidates.Any(candidate => candidate.HasVpnDetectedTag);
            if (isDetected == hasVpnDetectedTag)
            {
                continue;
            }

            var mutation = await repositoryApiClient.Players.V1
                .SetVpnDetectedTag(playerCandidates.Key, new SetVpnDetectedTagDto(isDetected))
                .ConfigureAwait(false);
            if (!mutation.IsSuccess)
            {
                logger.LogWarning(
                    "Failed to reconcile vpn-detected tag for player {PlayerId}. Status: {StatusCode}",
                    playerCandidates.Key,
                    mutation.StatusCode);
                playersSkipped++;
                continue;
            }

            if (isDetected)
            {
                tagsAdded++;
            }
            else
            {
                tagsRemoved++;
            }
        }

        return new VpnDetectedTagReconciliationSummary(candidates.Count, candidates.Select(candidate => candidate.PlayerId).Distinct().Count(), tagsAdded, tagsRemoved, playersSkipped);
    }

    private async Task<List<VpnDetectedTagReconciliationCandidateDto>> GetCandidatesAsync(
        DateTime cutoffUtc,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var candidates = new List<VpnDetectedTagReconciliationCandidateDto>();
        DateTime? afterLastUsedUtc = null;
        Guid? afterPlayerIpAddressId = null;

        while (true)
        {
            var pageResult = await repositoryApiClient.Players.V1
                .GetVpnDetectedTagReconciliationCandidates(cutoffUtc, afterLastUsedUtc, afterPlayerIpAddressId, pageSize)
                .ConfigureAwait(false);
            if (!pageResult.IsSuccess || pageResult.Result?.Data is null)
            {
                throw new InvalidOperationException($"Failed to retrieve VPN reconciliation candidates. Status: {pageResult.StatusCode}");
            }

            var page = pageResult.Result.Data;
            candidates.AddRange(page.Candidates);
            if (!page.NextLastUsedUtc.HasValue || !page.NextPlayerIpAddressId.HasValue)
            {
                return candidates;
            }

            afterLastUsedUtc = page.NextLastUsedUtc;
            afterPlayerIpAddressId = page.NextPlayerIpAddressId;
        }
    }

    private static async Task<Dictionary<string, IpIntelligenceDto>> GetIntelligenceAsync(
        IGeoLocationApiClient geoLocationApiClient,
        IEnumerable<VpnDetectedTagReconciliationCandidateDto> candidates,
        CancellationToken cancellationToken)
    {
        Dictionary<string, IpIntelligenceDto> intelligenceByAddress = new(StringComparer.OrdinalIgnoreCase);
        var addresses = candidates
            .Select(candidate => candidate.IpAddress)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var batch in addresses.Chunk(MaxGeoLocationBatchSize))
        {
            var result = await geoLocationApiClient.GeoLookup.V1_1
                .GetIpIntelligences([.. batch], cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess || result.Result?.Data?.Items is null)
            {
                continue;
            }

            foreach (var intelligence in result.Result.Data.Items)
            {
                if (!string.IsNullOrWhiteSpace(intelligence.Address) && intelligence.ProxyCheck is not null)
                {
                    intelligenceByAddress[intelligence.Address] = intelligence;
                }
            }
        }

        return intelligenceByAddress;
    }
}