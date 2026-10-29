
using System.Security.Claims;
using BattleHub.Trivia.Api.Hubs;
using BattleHub.Trivia.Api.Matchmaking;
using BattleHub.Trivia.Api.Profiles;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Interfaces;
using BattleHub.Trivia.Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BattleHub.Trivia.UnitTests;

public sealed class TriviaHubTests
{
    private static TriviaSessions CreateSessions(IServiceScopeFactory scopes,
        IHubContext<TriviaHub>? hubContext = null)
    {
        return new TriviaSessions(
            scopes,
            hubContext ?? Substitute.For<IHubContext<TriviaHub>>(),
            TimeProvider.System,
            new TriviaGameService(new TriviaScoringService()),
            NullLogger<TriviaSessions>.Instance);
    }

    private static ClaimsPrincipal CreateUser(string userId)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(
            [new Claim("sub", userId)],
            "TestAuth"));
    }

    private static IProfileGateway AllowedProfiles()
    {
        var profiles = Substitute.For<IProfileGateway>();

        profiles
            .CanPlayAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        return profiles;
    }

    private sealed class HttpFeature(HttpContext http) : IHttpContextFeature
    {
        public HttpContext? HttpContext { get; set; } = http;
    }

    [Fact]
    public async Task JoinMatchRejectsPlayerWithoutTriviaPermission()
    {
        using var services = new ServiceCollection()
            .BuildServiceProvider();

        var sessions = CreateSessions(
            services.GetRequiredService<IServiceScopeFactory>());

        var matchmaking = Substitute.For<IMatchmakingGateway>();
        var profiles = Substitute.For<IProfileGateway>();
        var context = Substitute.For<HubCallerContext>();

        var http = new DefaultHttpContext();
        http.Request.Headers.Authorization = "Bearer shell-token";

        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new HttpFeature(http));

        context.Items.Returns(new Dictionary<object, object?>());
        context.User.Returns(CreateUser("player-one"));
        context.Features.Returns(features);

        profiles
            .CanPlayAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var hub = new TriviaHub(sessions, matchmaking, profiles)
        {
            Context = context
        };

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinMatch(new JoinRequest("match-one", "room-token")));

        Assert.Equal("PERMISSION_DENIED", exception.Message);

        await profiles.Received(1).CanPlayAsync(
            "shell-token",
            Arg.Any<CancellationToken>());

        await matchmaking.DidNotReceiveWithAnyArgs()
            .ValidateAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task SubmitAnswerRejectsPlayerWithoutMatch()
    {
        using var services = new ServiceCollection()
            .BuildServiceProvider();

        var sessions = CreateSessions(
            services.GetRequiredService<IServiceScopeFactory>());

        var matchmaking = Substitute.For<IMatchmakingGateway>();
        var context = Substitute.For<HubCallerContext>();

        context.Items.Returns(new Dictionary<object, object?>());

        var hub = new TriviaHub(sessions, matchmaking, AllowedProfiles())
        {
            Context = context
        };

        var request = new AnswerRequest("match", "q1", "a");

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.SubmitAnswer(request));

        Assert.Equal("PLAYER_NOT_IN_MATCH", exception.Message);
    }

    [Fact]
    public async Task SubmitAnswerRejectsDifferentMatch()
    {
        using var services = new ServiceCollection()
            .BuildServiceProvider();

        var sessions = CreateSessions(
            services.GetRequiredService<IServiceScopeFactory>());

        var matchmaking = Substitute.For<IMatchmakingGateway>();
        var context = Substitute.For<HubCallerContext>();

        context.Items.Returns(
            new Dictionary<object, object?>
            {
                ["room"] = "match-one"
            });

        var hub = new TriviaHub(sessions, matchmaking, AllowedProfiles())
        {
            Context = context
        };

        var request = new AnswerRequest("match-two", "q1", "a");

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.SubmitAnswer(request));

        Assert.Equal("PLAYER_NOT_IN_MATCH", exception.Message);
    }

    [Fact]
    public async Task JoinMatchRejectsDifferentMatch()
    {
        using var services = new ServiceCollection()
            .BuildServiceProvider();

        var sessions = CreateSessions(
            services.GetRequiredService<IServiceScopeFactory>());

        var matchmaking = Substitute.For<IMatchmakingGateway>();
        var context = Substitute.For<HubCallerContext>();

        context.Items.Returns(
            new Dictionary<object, object?>
            {
                ["room"] = "match-one"
            });

        var hub = new TriviaHub(sessions, matchmaking, AllowedProfiles())
        {
            Context = context
        };

        var request = new JoinRequest("match-two", "room-token");

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinMatch(request));

        Assert.Equal("PLAYER_NOT_IN_MATCH", exception.Message);

        await matchmaking.DidNotReceiveWithAnyArgs()
            .ValidateAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task JoinMatchRejectsInvalidMatchmakingToken()
    {
        using var services = new ServiceCollection()
            .BuildServiceProvider();

        var sessions = CreateSessions(
            services.GetRequiredService<IServiceScopeFactory>());

        var matchmaking = Substitute.For<IMatchmakingGateway>();
        var context = Substitute.For<HubCallerContext>();

        context.Items.Returns(new Dictionary<object, object?>());
        context.User.Returns(CreateUser("player-one"));

        matchmaking
            .ValidateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<MatchRoom>(
                new HubException("MATCH_NOT_FOUND")));

        var hub = new TriviaHub(sessions, matchmaking, AllowedProfiles())
        {
            Context = context
        };

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinMatch(
                new JoinRequest("match-one", "invalid-token")));

        Assert.Equal("MATCH_NOT_FOUND", exception.Message);
    }

    [Fact]
    public async Task OnDisconnectedWithoutMatchDoesNotFail()
    {
        using var services = new ServiceCollection()
            .BuildServiceProvider();

        var sessions = CreateSessions(
            services.GetRequiredService<IServiceScopeFactory>());

        var matchmaking = Substitute.For<IMatchmakingGateway>();
        var context = Substitute.For<HubCallerContext>();

        context.Items.Returns(new Dictionary<object, object?>());

        var hub = new TriviaHub(sessions, matchmaking, AllowedProfiles())
        {
            Context = context
        };

        await hub.OnDisconnectedAsync(null);
    }

    [Fact]
    public async Task JoinMatchAddsPlayerToSignalRGroup()
    {
        var results = Substitute.For<IResultRepository>();

        results
            .GetByMatchIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<MatchResult?>(null));

        var questions = Substitute.For<IQuestionRepository>();

        IReadOnlyList<Question> bank = Enumerable.Range(1, 10)
            .Select(i => new Question
            {
                QuestionId = "q" + i,
                Text = "Pregunta " + i,
                Category = "General",
                CorrectAnswerId = "a",
                Options = new[] { "a", "b", "c", "d" }
                    .Select(id => new AnswerOption
                    {
                        AnswerId = id,
                        Text = id
                    })
                    .ToArray()
            })
            .ToArray();

        questions
            .DrawAsync(
                Arg.Any<int>(),
                Arg.Any<QuestionFilter?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(bank));

        using var services = new ServiceCollection()
            .AddSingleton<IResultRepository>(results)
            .AddSingleton<IQuestionRepository>(questions)
            .BuildServiceProvider();

        var client = Substitute.For<ISingleClientProxy>();
        var hubClients = Substitute.For<IHubClients>();

        hubClients
            .Client(Arg.Any<string>())
            .Returns(client);

        hubClients
            .Group(Arg.Any<string>())
            .Returns(client);

        var hubContext = Substitute.For<IHubContext<TriviaHub>>();
        hubContext.Clients.Returns(hubClients);

        var sessions = CreateSessions(
            services.GetRequiredService<IServiceScopeFactory>(),
            hubContext);

        var matchmaking = Substitute.For<IMatchmakingGateway>();
        var context = Substitute.For<HubCallerContext>();
        var groups = Substitute.For<IGroupManager>();

        context.Items.Returns(new Dictionary<object, object?>());
        context.ConnectionId.Returns("connection-one");
        context.User.Returns(CreateUser("player-one"));

        var room = new MatchRoom(
            "match-one",
            "trivia",
            "Started",
            [new RoomPlayer("player-one", "Uno")]);

        matchmaking
            .ValidateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(room));

        var hub = new TriviaHub(sessions, matchmaking, AllowedProfiles())
        {
            Context = context,
            Groups = groups
        };

        await hub.JoinMatch(
            new JoinRequest("match-one", "room-token"));

        await matchmaking.Received(1).ValidateAsync(
            "match-one",
            "player-one",
            "room-token",
            Arg.Any<CancellationToken>());

        await groups.Received(1).AddToGroupAsync(
            "connection-one",
            "match-one",
            Arg.Any<CancellationToken>());

        Assert.Equal("match-one", context.Items["room"]);

        await hub.SubmitAnswer(
           new AnswerRequest(
           "match-one",
            "q1",
         "a"));
    }

    [Fact]
    public async Task JoinMatchRemovesPlayerFromGroupWhenSessionFails()
    {
        var results = Substitute.For<IResultRepository>();

        results
            .GetByMatchIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<MatchResult?>(null));

        var questions = Substitute.For<IQuestionRepository>();

        IReadOnlyList<Question> bank = Enumerable.Range(1, 10)
            .Select(i => new Question
            {
                QuestionId = "q" + i,
                Text = "Pregunta " + i,
                Category = "General",
                CorrectAnswerId = "a",
                Options = new[] { "a", "b", "c", "d" }
                    .Select(id => new AnswerOption
                    {
                        AnswerId = id,
                        Text = id
                    })
                    .ToArray()
            })
            .ToArray();

        questions
            .DrawAsync(
                Arg.Any<int>(),
                Arg.Any<QuestionFilter?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(bank));

        using var services = new ServiceCollection()
            .AddSingleton<IResultRepository>(results)
            .AddSingleton<IQuestionRepository>(questions)
            .BuildServiceProvider();

        var sessions = CreateSessions(
            services.GetRequiredService<IServiceScopeFactory>());

        var matchmaking = Substitute.For<IMatchmakingGateway>();
        var context = Substitute.For<HubCallerContext>();
        var groups = Substitute.For<IGroupManager>();

        context.Items.Returns(new Dictionary<object, object?>());
        context.ConnectionId.Returns("connection-one");
        context.User.Returns(CreateUser("player-one"));

        var room = new MatchRoom(
            "match-one",
            "trivia",
            "Started",
            [new RoomPlayer("player-two", "Dos")]);

        matchmaking
            .ValidateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(room));

        var hub = new TriviaHub(sessions, matchmaking, AllowedProfiles())
        {
            Context = context,
            Groups = groups
        };

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinMatch(
                new JoinRequest("match-one", "room-token")));

        Assert.Equal("PLAYER_NOT_IN_MATCH", exception.Message);

        await groups.Received(1).AddToGroupAsync(
            "connection-one",
            "match-one",
            Arg.Any<CancellationToken>());

        await groups.Received(1).RemoveFromGroupAsync(
            "connection-one",
            "match-one",
            Arg.Any<CancellationToken>());

        Assert.False(context.Items.ContainsKey("room"));
    }

}
