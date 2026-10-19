
using System.Net;
using System.Net.Http.Json;
using BattleHub.Trivia.Api.Matchmaking;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BattleHub.Trivia.UnitTests;

public sealed class MatchmakingGatewayTests
{
    private sealed class Handler(
        Func<HttpRequestMessage, HttpResponseMessage> respond
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct)
        {
            return Task.FromResult(respond(request));
        }
    }

    private sealed class FakeTokenProvider : IMatchmakingTokenProvider
    {
        public Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult("machine-token");
        }
    }

    private static IConfiguration Config =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Matchmaking:BaseUrl"] = "http://localhost:5211"
            })
            .Build();

    [Fact]
    public async Task ForwardsRoomTokenAndRejectsNonParticipant()
    {
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("room-token", request.Headers.Authorization.Parameter);
            Assert.Equal("/api/matches/room", request.RequestUri!.AbsolutePath);

            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new MatchRoom(
                        "room",
                        "trivia",
                        "Started",
                        [new("player-one", "Uno")]))
            };
        }));

        var gateway = new MatchmakingGateway(
            http,
            Config,
            TimeProvider.System,
            NullLogger<MatchmakingGateway>.Instance,
            new FakeTokenProvider());

        Assert.Equal(
            "room",
            (await gateway.ValidateAsync(
                "room",
                "player-one",
                "room-token",
                default)).Id);

        await Assert.ThrowsAsync<HubException>(() =>
            gateway.ValidateAsync(
                "room",
                "player-intruder",
                "room-token",
                default));
    }

    [Theory]
    [InlineData("typing", "Started")]
    [InlineData("trivia", "Waiting")]
    [InlineData("trivia", "Cancelled")]
    public async Task RejectsWrongGameOrStatus(string game, string status)
    {
        using var http = new HttpClient(new Handler(_ =>
            new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new MatchRoom(
                        "room",
                        game,
                        status,
                        [new("player-one", "Uno")]))
            }));

        var gateway = new MatchmakingGateway(
            http,
            Config,
            TimeProvider.System,
            NullLogger<MatchmakingGateway>.Instance,
            new FakeTokenProvider());

        await Assert.ThrowsAsync<HubException>(() =>
            gateway.ValidateAsync(
                "room",
                "player-one",
                "room-token",
                default));
    }

    [Theory]
    [InlineData(204, FinishOutcome.Confirmed)]
    [InlineData(401, FinishOutcome.Rejected)]
    [InlineData(403, FinishOutcome.Rejected)]
    [InlineData(409, FinishOutcome.Rejected)]
    public async Task FinishUsesMachineTokenAndDoesNotRetryPermanentRejection(
        int code,
        FinishOutcome expected)
    {
        var calls = 0;

        using var http = new HttpClient(new Handler(request =>
        {
            calls++;

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Null(request.Content);
            Assert.Equal(
                "/api/matches/room/finish",
                request.RequestUri!.AbsolutePath);
            Assert.Equal(
                "Bearer",
                request.Headers.Authorization!.Scheme);
            Assert.Equal(
                "machine-token",
                request.Headers.Authorization.Parameter);

            return new((HttpStatusCode)code);
        }));

        var gateway = new MatchmakingGateway(
            http,
            Config,
            TimeProvider.System,
            NullLogger<MatchmakingGateway>.Instance,
            new FakeTokenProvider());

        Assert.Equal(
            expected,
            await gateway.FinishAsync("room", default));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task FinishRetriesTransientFailuresOnlyThreeTimes()
    {
        var calls = 0;

        using var http = new HttpClient(new Handler(request =>
        {
            calls++;

            Assert.Equal(
                "/api/matches/room/finish",
                request.RequestUri!.AbsolutePath);
            Assert.Equal(
                "machine-token",
                request.Headers.Authorization!.Parameter);

            return new(HttpStatusCode.ServiceUnavailable);
        }));

        var gateway = new MatchmakingGateway(
            http,
            Config,
            TimeProvider.System,
            NullLogger<MatchmakingGateway>.Instance,
            new FakeTokenProvider());

        Assert.Equal(
            FinishOutcome.Pending,
            await gateway.FinishAsync("room", default));

        Assert.Equal(3, calls);
    }
}
