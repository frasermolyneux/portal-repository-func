using System.Net;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Notifications;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Repository.App.Functions;

public class UnclaimedActionReminder(
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
        log.LogInformation("Checking for unclaimed admin actions to send reminders");

        // Note: UnclaimedActions matches all action types (bans, temp bans, kicks, etc.) without a UserProfile.
        var unclaimedResult = await repositoryApiClient.AdminActions.V1
            .GetAdminActions(null, null, null, AdminActionFilter.UnclaimedActions, 0, 50, AdminActionOrder.CreatedDesc)
            .ConfigureAwait(false);

        if (unclaimedResult.Result?.Data?.Items is null || !unclaimedResult.Result.Data.Items.Any())
        {
            log.LogInformation("No unclaimed admin actions found");
            return;
        }

        var unclaimedActions = unclaimedResult.Result.Data.Items.ToList();
        log.LogInformation("Found {Count} unclaimed admin actions", unclaimedActions.Count);

        if (unclaimedActions.Count >= 50)
            log.LogWarning("Unclaimed actions query hit page limit of 50; some actions may not trigger reminders");

        // Get all admin users to notify head admins
        const int headAdminPageSize = 200;
        var adminsResult = await repositoryApiClient.UserProfiles.V1
            .GetUserProfiles(null, UserProfileFilter.HeadAdmins, 0, headAdminPageSize, null)
            .ConfigureAwait(false);

        if (adminsResult.Result?.Data?.Items is null || !adminsResult.Result.Data.Items.Any())
        {
            log.LogInformation("No head admins found to notify");
            return;
        }

        var adminItems = adminsResult.Result.Data.Items;
        if (adminItems.Count() >= headAdminPageSize)
            log.LogWarning("Head admin query returned {Count} results (page limit {PageSize}); some admins may not receive reminders", adminItems.Count(), headAdminPageSize);

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

            // Find head admins and senior admins for this game type
            var recipients = adminItems
                .Where(up => up.UserProfileClaims.Any(c =>
                    c.ClaimType == UserProfileClaimType.SeniorAdmin ||
                    (c.ClaimType == UserProfileClaimType.HeadAdmin && c.ClaimValue == gameTypeString)))
                .ToList();

            if (recipients.Count == 0)
                continue;

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

            log.LogInformation("Sent unclaimed action reminders for {GameType} to {Count} recipients", gameType, recipients.Count);
        }

        log.LogInformation("Unclaimed action reminder processing completed");
    }
}
