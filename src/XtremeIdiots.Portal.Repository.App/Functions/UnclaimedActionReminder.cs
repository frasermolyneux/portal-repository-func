using System.Net;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Notifications;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.UserProfiles;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Repository.App.Functions;

public partial class UnclaimedActionReminder(
    ILogger<UnclaimedActionReminder> log,
    IRepositoryApiClient repositoryApiClient)
{
    [Function(nameof(RunUnclaimedActionReminderHttp))]
    public async Task<HttpResponseData> RunUnclaimedActionReminderHttp([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req, FunctionContext context)
    {
        await RunUnclaimedActionReminder(null).ConfigureAwait(false);
        return req.CreateResponse(HttpStatusCode.OK);
    }

    [Function(nameof(RunUnclaimedActionReminder))]
    public async Task RunUnclaimedActionReminder([TimerTrigger("0 0 */6 * * *")] TimerInfo? myTimer)
    {
        LogCheckingForUnclaimedAdminActions(log);

        // Note: UnclaimedActions matches all action types (bans, temp bans, kicks, etc.) without a UserProfile.
        var unclaimedResult = await repositoryApiClient.AdminActions.V1
            .GetAdminActions(null, null, null, AdminActionFilter.UnclaimedActions, 0, 50, AdminActionOrder.CreatedDesc)
            .ConfigureAwait(false);

        if (unclaimedResult.Result?.Data?.Items is null || !unclaimedResult.Result.Data.Items.Any())
        {
            LogNoUnclaimedAdminActions(log);
            return;
        }

        var unclaimedActions = unclaimedResult.Result.Data.Items.ToList();
        LogFoundUnclaimedAdminActions(log, unclaimedActions.Count);

        if (unclaimedActions.Count >= 50)
        {
            LogUnclaimedActionsPageLimitReached(log);
        }

        // Get all admin users to notify. Uses AnyAdmin so global admins (Webmaster / SeniorAdmin)
        // are included even when they hold no game-scoped HeadAdmin claim; per-game-type recipients
        // are then selected from this set below.
        const int adminPageSize = 200;
        var adminsResult = await repositoryApiClient.UserProfiles.V1
            .GetUserProfiles(null, UserProfileFilter.AnyAdmin, 0, adminPageSize, null)
            .ConfigureAwait(false);

        if (adminsResult.Result?.Data?.Items is null || !adminsResult.Result.Data.Items.Any())
        {
            LogNoAdminsFound(log);
            return;
        }

        var adminItems = adminsResult.Result.Data.Items;
        if (adminItems.Count() >= adminPageSize)
        {
            LogAdminQueryPageLimitReached(log, adminItems.Count(), adminPageSize);
        }

        // Group unclaimed actions by game type for targeted notifications
        var actionsByGameType = unclaimedActions
            .Where(a => a.Player?.GameType is not null)
            .GroupBy(a => a.Player!.GameType)
            .ToList();

        foreach (var group in actionsByGameType)
        {
            var gameType = group.Key;
            var count = group.Count();
            var gameTypeString = gameType.ToString();

            // Find head admins, senior admins and webmasters for this game type
            var recipients = adminItems
                .Where(up => up.UserProfileClaims.Any(c =>
                    c.ClaimType == UserProfileClaimType.Webmaster ||
                    c.ClaimType == UserProfileClaimType.SeniorAdmin ||
                    (c.ClaimType == UserProfileClaimType.HeadAdmin && c.ClaimValue == gameTypeString)))
                .ToList();

            if (recipients.Count == 0)
            {
                continue;
            }

            await SendRemindersForGameType(gameType, count, recipients).ConfigureAwait(false);
        }

        LogProcessingCompleted(log);
    }

    private async Task SendRemindersForGameType(GameType gameType, int count, List<UserProfileDto> recipients)
    {
        var title = $"{count} Unclaimed Action{(count > 1 ? "s" : "")} on {gameType}";
        var message = $"There {(count > 1 ? "are" : "is")} {count} unclaimed admin action{(count > 1 ? "s" : "")} that need{(count == 1 ? "s" : "")} review.";

        foreach (var recipient in recipients)
        {
            try
            {
                var dto = new CreateNotificationDto(
                    recipient.UserProfileId,
                    "unclaimed-action-reminder",
                    title,
                    message)
                {
                    ActionUrl = "/AdminActions/Unclaimed"
                };

                await repositoryApiClient.Notifications.V1.CreateNotification(dto).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to create unclaimed action reminder for user {UserProfileId}", recipient.UserProfileId);
            }
        }

        LogRemindersSent(log, gameType, recipients.Count);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Checking for unclaimed admin actions to send reminders")]
    private static partial void LogCheckingForUnclaimedAdminActions(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "No unclaimed admin actions found")]
    private static partial void LogNoUnclaimedAdminActions(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Found {Count} unclaimed admin actions")]
    private static partial void LogFoundUnclaimedAdminActions(ILogger logger, int count);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Unclaimed actions query hit page limit of 50; some actions may not trigger reminders")]
    private static partial void LogUnclaimedActionsPageLimitReached(ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "No admins found to notify")]
    private static partial void LogNoAdminsFound(ILogger logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning, Message = "Admin query returned {Count} results (page limit {PageSize}); some admins may not receive reminders")]
    private static partial void LogAdminQueryPageLimitReached(ILogger logger, int count, int pageSize);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Sent unclaimed action reminders for {GameType} to {Count} recipients")]
    private static partial void LogRemindersSent(ILogger logger, GameType gameType, int count);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Unclaimed action reminder processing completed")]
    private static partial void LogProcessingCompleted(ILogger logger);
}
