using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using MX.Api.Abstractions;
using MX.Api.Client;
using Newtonsoft.Json;
using RestSharp;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.AdminActions;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Notifications;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.UserProfiles;
using XtremeIdiots.Portal.Repository.Api.Client.V1;
using XtremeIdiots.Portal.Repository.App.Functions;

namespace XtremeIdiots.Portal.Repository.App.Tests.Functions;

public sealed class UnclaimedActionReminderTests
{
    private readonly Mock<IRepositoryApiClient> client = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<ILogger<UnclaimedActionReminder>> logger = new();
    private readonly List<CreateNotificationDto> notifications = [];
    private List<AdminActionDto> actions = [Action(GameType.CallOfDuty4)];
    private List<UserProfileDto> admins = [Admin(UserProfileClaimType.Webmaster)];

    public UnclaimedActionReminderTests()
    {
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        Mock.Get(client.Object.AdminActions.V1).Setup(x => x.GetAdminActions(null, null, null, AdminActionFilter.UnclaimedActions, 0, 50, AdminActionOrder.CreatedDesc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Page(actions));
        Mock.Get(client.Object.UserProfiles.V1).Setup(x => x.GetUserProfiles(null, UserProfileFilter.AnyAdmin, 0, 200, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Page(admins));
        Mock.Get(client.Object.Notifications.V1).Setup(x => x.CreateNotification(It.IsAny<CreateNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<CreateNotificationDto, CancellationToken>((dto, _) => notifications.Add(dto)).ReturnsAsync(new ApiResult(HttpStatusCode.OK));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Run_QueryException_PropagatesWithoutSending(bool adminQuery)
    {
        var failure = new InvalidOperationException("query failed");
        Mock.Get(client.Object.UserProfiles.V1).Setup(x => x.GetUserProfiles(null, UserProfileFilter.AnyAdmin, 0, 200, null, It.IsAny<CancellationToken>()))
            .Returns(() => adminQuery ? Task.FromException<ApiResult<CollectionModel<UserProfileDto>>>(failure) : Task.FromResult(Page(admins)));
        Mock.Get(client.Object.AdminActions.V1).Setup(x => x.GetAdminActions(null, null, null, AdminActionFilter.UnclaimedActions, 0, 50, AdminActionOrder.CreatedDesc, It.IsAny<CancellationToken>()))
            .Returns(() => adminQuery ? Task.FromResult(Page(actions)) : Task.FromException<ApiResult<CollectionModel<AdminActionDto>>>(failure));
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(Run));
        Mock.Get(client.Object.Notifications.V1).Verify(x => x.CreateNotification(It.IsAny<CreateNotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task Run_SelectsRecipientsAndPreservesContentDespiteRecipientFailure(int count, bool fail)
    {
        actions = [.. Enumerable.Repeat(Action(GameType.CallOfDuty4), count), Action(GameType.CallOfDuty5), Json<AdminActionDto>(new { })];
        admins = [Admin(UserProfileClaimType.HeadAdmin, "CallOfDuty4"), Admin(UserProfileClaimType.SeniorAdmin), Admin(UserProfileClaimType.Webmaster),
            Admin(UserProfileClaimType.HeadAdmin, "CallOfDuty2"), Admin(UserProfileClaimType.GameAdmin, "CallOfDuty4"), Json<UserProfileDto>(new { UserProfileClaims = Array.Empty<object>() })];
        admins[0].UserProfileClaims.Add(admins[0].UserProfileClaims[0]);
        Mock.Get(client.Object.Notifications.V1).Setup(x => x.CreateNotification(It.IsAny<CreateNotificationDto>(), It.IsAny<CancellationToken>()))
            .Callback<CreateNotificationDto, CancellationToken>((dto, _) => notifications.Add(dto))
            .Returns((CreateNotificationDto dto, CancellationToken _) => fail && dto.UserProfileId == admins[0].UserProfileId ? Task.FromException<ApiResult>(new InvalidOperationException("notification failed")) : Task.FromResult(new ApiResult(HttpStatusCode.OK)));
        await Run();
        Assert.Equal([admins[0].UserProfileId, admins[1].UserProfileId, admins[2].UserProfileId, admins[1].UserProfileId, admins[2].UserProfileId], notifications.Select(n => n.UserProfileId));
        Assert.All(notifications.Take(3), n => AssertContent(n, count, GameType.CallOfDuty4));
        Assert.All(notifications.Skip(3), n => AssertContent(n, 1, GameType.CallOfDuty5));
        AssertLog(LogLevel.Information, 7, "Sent unclaimed action reminders for CallOfDuty4 to 3 recipients");
        AssertLog(LogLevel.Information, 8, "Unclaimed action reminder processing completed");
        Assert.Equal(fail ? 1 : 0, logger.Invocations.Count(i => i.Method.Name == "Log" && i.Arguments[0] is LogLevel.Error && i.Arguments[3] is InvalidOperationException && i.Arguments[2]?.ToString() == $"Failed to create unclaimed action reminder for user {admins[0].UserProfileId}"));
        Mock.Get(client.Object.AdminActions.V1).VerifyAll();
        Mock.Get(client.Object.UserProfiles.V1).VerifyAll();
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Run_EmptyOrMissingResultsAndIneligibleGroups_DoNotNotify(int scenario)
    {
        actions = scenario < 2 ? [] : scenario == 5 ? [Json<AdminActionDto>(new { })] : actions;
        admins = scenario is 2 or 3 ? [] : scenario == 4 ? [Admin(UserProfileClaimType.HeadAdmin, "CallOfDuty5")] : admins;
        Mock.Get(client.Object.AdminActions.V1).Setup(x => x.GetAdminActions(null, null, null, AdminActionFilter.UnclaimedActions, 0, 50, AdminActionOrder.CreatedDesc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario == 1 ? new ApiResult<CollectionModel<AdminActionDto>>(HttpStatusCode.ServiceUnavailable) : Page(actions));
        Mock.Get(client.Object.UserProfiles.V1).Setup(x => x.GetUserProfiles(null, UserProfileFilter.AnyAdmin, 0, 200, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scenario == 3 ? new ApiResult<CollectionModel<UserProfileDto>>(HttpStatusCode.ServiceUnavailable) : Page(admins));
        await Run();
        Assert.Empty(notifications);
        AssertLog(LogLevel.Information, scenario < 2 ? 2 : scenario < 4 ? 5 : 8, scenario < 2 ? "No unclaimed admin actions found" : scenario < 4 ? "No admins found to notify" : "Unclaimed action reminder processing completed");
        Mock.Get(client.Object.UserProfiles.V1).Verify(x => x.GetUserProfiles(null, UserProfileFilter.AnyAdmin, 0, 200, null, It.IsAny<CancellationToken>()), scenario < 2 ? Times.Never : Times.Once);
    }
    [Theory]
    [InlineData(49, 199)]
    [InlineData(49, 200)]
    [InlineData(50, 199)]
    [InlineData(50, 200)]
    public async Task Run_QueryLimitsEmitStructuredWarningsAtBoundary(int actionCount, int adminCount)
    {
        actions = [.. Enumerable.Repeat(Json<AdminActionDto>(new { }), actionCount)];
        admins = [.. Enumerable.Repeat(Admin(UserProfileClaimType.GameAdmin), adminCount)];
        await Run();
        Assert.Equal((actionCount == 50 ? 1 : 0) + (adminCount == 200 ? 1 : 0), logger.Invocations.Count(i => i.Method.Name == "Log" && i.Arguments[0] is LogLevel.Warning));
        if (actionCount == 50)
        {
            AssertLog(LogLevel.Warning, 4, "Unclaimed actions query hit page limit of 50; some actions may not trigger reminders");
        }
        if (adminCount == 200)
        {
            AssertLog(LogLevel.Warning, 6, "Admin query returned 200 results (page limit 200); some admins may not receive reminders");
        }
        AssertLog(LogLevel.Information, 3, $"Found {actionCount} unclaimed admin actions");
        Assert.Empty(notifications);
    }
    [Fact]
    public async Task Run_RealNotificationClient_SendsExpectedHttpPayload()
    {
        using var handler = new Mock<HttpMessageHandler>().Object;
        HttpRequestMessage? sent = null;
        CreateNotificationDto? payload = null;
        Mock.Get(handler).Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken _) =>
            {
                sent = request;
                payload = JsonConvert.DeserializeObject<CreateNotificationDto>(await request.Content!.ReadAsStringAsync(_));
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        var services = new ServiceCollection().AddLogging();
        services.AddRepositoryApiClient(o => o.WithBaseUrl("https://repository.example.com").WithApiKeyAuthentication("test-only", "api-key"));
        using var rest = new RestClient(new HttpClient(handler), new RestClientOptions("https://repository.example.com"), disposeHttpClient: true);
        var transport = new Mock<IRestClientService>();
        transport.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .Returns((string _, RestRequest request, CancellationToken ct) => rest.ExecuteAsync(request, ct));
        transport.Setup(x => x.ExecuteWithNamedOptionsAsync(It.IsAny<string>(), It.IsAny<RestRequest>(), It.IsAny<CancellationToken>()))
            .Returns((string _, RestRequest request, CancellationToken ct) => rest.ExecuteAsync(request, ct));
        services.AddSingleton(transport.Object);
        using var provider = services.BuildServiceProvider();
        client.Setup(x => x.Notifications.V1).Returns(provider.GetRequiredService<IRepositoryApiClient>().Notifications.V1);
        await Run();
        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.NotNull(payload);
        Assert.Equal(admins[0].UserProfileId, payload.UserProfileId);
        AssertContent(payload, 1, GameType.CallOfDuty4);
        Mock.Get(handler).Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    private Task Run() => new UnclaimedActionReminder(logger.Object, client.Object).RunUnclaimedActionReminder(null);
    private static T Json<T>(object value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value))!;
    private static AdminActionDto Action(GameType game) => Json<AdminActionDto>(new { Player = new { GameType = game } });
    private static UserProfileDto Admin(string role, string? game = null) => Json<UserProfileDto>(new { UserProfileId = Guid.NewGuid(), UserProfileClaims = new[] { new { ClaimType = role, ClaimValue = game } } });
    private static ApiResult<CollectionModel<T>> Page<T>(List<T> items) => new(HttpStatusCode.OK, new ApiResponse<CollectionModel<T>>(new CollectionModel<T>(items)));
    private void AssertLog(LogLevel level, int id, string message) => Assert.Contains(logger.Invocations, i => i.Method.Name == "Log" && i.Arguments[0] is LogLevel actual && actual == level && i.Arguments[1] is EventId e && e.Id == id && i.Arguments[2]?.ToString() == message);
    private static void AssertContent(CreateNotificationDto dto, int count, GameType game)
    {
        Assert.Equal("unclaimed-action-reminder", dto.NotificationTypeId);
        Assert.Equal("/AdminActions/Unclaimed", dto.ActionUrl);
        Assert.Equal($"{count} Unclaimed Action{(count > 1 ? "s" : "")} on {game}", dto.Title);
        Assert.Equal(count == 1 ? "There is 1 unclaimed admin action that needs review." : $"There are {count} unclaimed admin actions that need review.", dto.Message);
    }
}
