
using BattleHub.Trivia.Api.Hubs;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Interfaces;
using BattleHub.Trivia.Domain.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BattleHub.Trivia.UnitTests;

public sealed class TriviaHubTests
{
    private static readonly RoomPlayer[] OnePlayer = [new("player-one", "Uno")];

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

    // Banco de diez preguntas y ninguna partida guardada: suficiente para crear una sesión real.
    private static ServiceProvider CreateGameServices()
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

        return new ServiceCollection()
            .AddSingleton(results)
            .AddSingleton(questions)
            .BuildServiceProvider();
    }

    private static IHubContext<TriviaHub> CreateHubContext()
    {
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

        return hubContext;
    }

    private static HubCallerContext CreateContext(
        Dictionary<object, object?>? items = null,
        string connectionId = "connection-one")
    {
        var context = Substitute.For<HubCallerContext>();

        context.Items.Returns(items ?? []);
        context.ConnectionId.Returns(connectionId);

        return context;
    }

    private static TriviaHub CreateHub(TriviaSessions sessions, HubCallerContext context, IGroupManager? groups = null) =>
        new(sessions)
        {
            Context = context,
            Groups = groups ?? Substitute.For<IGroupManager>()
        };

    private static TriviaSessions CreateUnusedSessions() =>
        CreateSessions(new ServiceCollection()
            .BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>());

    [Fact]
    public async Task SubmitAnswerRejectsPlayerWithoutMatch()
    {
        var hub = CreateHub(CreateUnusedSessions(), CreateContext());

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.SubmitAnswer(new AnswerRequest("match", "q1", "a")));

        Assert.Equal("PLAYER_NOT_IN_MATCH", exception.Message);
    }

    [Fact]
    public async Task SubmitAnswerRejectsDifferentMatch()
    {
        var context = CreateContext(new Dictionary<object, object?>
        {
            ["room"] = "match-one",
            ["user"] = "player-one"
        });
        var hub = CreateHub(CreateUnusedSessions(), context);

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.SubmitAnswer(new AnswerRequest("match-two", "q1", "a")));

        Assert.Equal("PLAYER_NOT_IN_MATCH", exception.Message);
    }

    [Fact]
    public async Task JoinMatchRejectsDifferentMatchOnSameConnection()
    {
        var groups = Substitute.For<IGroupManager>();
        var context = CreateContext(new Dictionary<object, object?>
        {
            ["room"] = "match-one"
        });
        var hub = CreateHub(CreateUnusedSessions(), context, groups);

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinMatch(new JoinRequest("match-two", "player-one", OnePlayer)));

        Assert.Equal("PLAYER_NOT_IN_MATCH", exception.Message);
        await groups.DidNotReceiveWithAnyArgs()
            .AddToGroupAsync(default!, default!, default);
    }

    [Fact]
    public async Task JoinMatchRejectsDifferentPlayerOnSameConnection()
    {
        var context = CreateContext(new Dictionary<object, object?>
        {
            ["room"] = "match-one",
            ["user"] = "player-one"
        });
        var hub = CreateHub(CreateUnusedSessions(), context);

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinMatch(new JoinRequest("match-one", "player-two", [new("player-one", "Uno"), new("player-two", "Dos")])));

        Assert.Equal("PLAYER_NOT_IN_MATCH", exception.Message);
    }

    public static TheoryData<string, string, RoomPlayer[]?, string> InvalidRooms => new()
    {
        { "", "player-one", OnePlayer, "MATCH_NOT_FOUND" },
        { "   ", "player-one", OnePlayer, "MATCH_NOT_FOUND" },
        { new string('m', 65), "player-one", OnePlayer, "MATCH_NOT_FOUND" },
        { "match", "player-one", null, "PLAYER_NOT_IN_MATCH" },
        { "match", "player-one", [], "PLAYER_NOT_IN_MATCH" },
        { "match", "ajeno", OnePlayer, "PLAYER_NOT_IN_MATCH" },
        { "match", "player-one", [new("player-one", "Uno"), new("player-one", "Otra vez")], "PLAYER_NOT_IN_MATCH" },
        { "match", "player-one", [new("player-one", " ")], "PLAYER_NOT_IN_MATCH" },
        { "match", " ", [new(" ", "Uno")], "PLAYER_NOT_IN_MATCH" },
        { "match", new string('u', 65), [new(new string('u', 65), "Uno")], "PLAYER_NOT_IN_MATCH" },
        { "match", "player-one", [new("player-one", new string('n', 101))], "PLAYER_NOT_IN_MATCH" },
        { "match", "p0", Enumerable.Range(0, 21).Select(i => new RoomPlayer("p" + i, "Jugador")).ToArray(), "PLAYER_NOT_IN_MATCH" }
    };

    [Theory]
    [MemberData(nameof(InvalidRooms))]
    public async Task JoinMatchRejectsInvalidRoom(string matchId, string userId, RoomPlayer[]? players, string expected)
    {
        var groups = Substitute.For<IGroupManager>();
        var hub = CreateHub(CreateUnusedSessions(), CreateContext(), groups);

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinMatch(new JoinRequest(matchId, userId, players!)));

        Assert.Equal(expected, exception.Message);
        await groups.DidNotReceiveWithAnyArgs()
            .AddToGroupAsync(default!, default!, default);
    }

    [Fact]
    public async Task OnDisconnectedWithoutMatchDoesNotFail()
    {
        var hub = CreateHub(CreateUnusedSessions(), CreateContext());

        await hub.OnDisconnectedAsync(null);
    }

    [Fact]
    public async Task JoinMatchAddsPlayerToSignalRGroupAndRemembersConnectionIdentity()
    {
        using var services = CreateGameServices();
        var sessions = CreateSessions(
            services.GetRequiredService<IServiceScopeFactory>(),
            CreateHubContext());
        var groups = Substitute.For<IGroupManager>();
        var context = CreateContext();
        var hub = CreateHub(sessions, context, groups);

        await hub.JoinMatch(new JoinRequest("match-one", "player-one", OnePlayer));

        await groups.Received(1).AddToGroupAsync(
            "connection-one",
            "match-one",
            Arg.Any<CancellationToken>());

        Assert.Equal("match-one", context.Items["room"]);
        Assert.Equal("player-one", context.Items["user"]);

        await hub.SubmitAnswer(new AnswerRequest("match-one", "q1", "a"));
    }

    [Fact]
    public async Task FirstJoinDefinesRosterAndLaterPlayerOutsideItIsRemovedFromGroup()
    {
        using var services = CreateGameServices();
        var sessions = CreateSessions(
            services.GetRequiredService<IServiceScopeFactory>(),
            CreateHubContext());

        await CreateHub(sessions, CreateContext())
            .JoinMatch(new JoinRequest("match-one", "player-one", OnePlayer));

        var groups = Substitute.For<IGroupManager>();
        var context = CreateContext(connectionId: "connection-two");
        var hub = CreateHub(sessions, context, groups);

        var exception = await Assert.ThrowsAsync<HubException>(
            () => hub.JoinMatch(new JoinRequest("match-one", "player-two", [new("player-two", "Dos")])));

        Assert.Equal("PLAYER_NOT_IN_MATCH", exception.Message);

        await groups.Received(1).AddToGroupAsync(
            "connection-two",
            "match-one",
            Arg.Any<CancellationToken>());

        await groups.Received(1).RemoveFromGroupAsync(
            "connection-two",
            "match-one",
            Arg.Any<CancellationToken>());

        Assert.False(context.Items.ContainsKey("room"));
        Assert.False(context.Items.ContainsKey("user"));
    }
}
