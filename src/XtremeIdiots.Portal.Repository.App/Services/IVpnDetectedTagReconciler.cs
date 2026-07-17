namespace XtremeIdiots.Portal.Repository.App.Services;

public interface IVpnDetectedTagReconciler
{
    Task<VpnDetectedTagReconciliationSummary> ReconcileAsync(bool force, CancellationToken cancellationToken = default);
}

public sealed record VpnDetectedTagReconciliationSummary(
    int Candidates,
    int PlayersEvaluated,
    int TagsAdded,
    int TagsRemoved,
    int PlayersSkipped);