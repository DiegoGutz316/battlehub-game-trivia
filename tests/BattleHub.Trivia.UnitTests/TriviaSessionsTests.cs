
using System.Text.Json;
using BattleHub.Trivia.Api.Hubs;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Interfaces;
using BattleHub.Trivia.Domain.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace BattleHub.Trivia.UnitTests;

public sealed class TriviaSessionsTests
{
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(int seconds) => now = now.AddSeconds(seconds);
    }

    private static IResultRepository CreateResults(List<MatchResult> saved)
    {
        var results = Substitute.For<IResultRepository>();

        results
            .SaveAsync(Arg.Any<MatchResult>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                saved.Add(call.ArgAt<MatchResult>(0));
                return Task.CompletedTask;
            });

        return results;
    }

    private static IQuestionRepository CreateQuestions()
    {
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

        questions
            .GetByIdAsync(
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var id = call.ArgAt<string>(0);
                return Task.FromResult(bank.FirstOrDefault(q => q.QuestionId == id));
            });

        return questions;
    }

    private sealed record Message(
        string Recipient,
        string Name,
        JsonElement Payload
    );

    private static ISingleClientProxy CreateProxy(
    string recipient,
    List<Message> messages)
{
    var proxy = Substitute.For<ISingleClientProxy>();

    proxy
        .SendCoreAsync(
            Arg.Any<string>(),
            Arg.Any<object?[]>(),
            Arg.Any<CancellationToken>())
        .Returns(call =>
        {
            var method = call.ArgAt<string>(0);
            var args = call.ArgAt<object?[]>(1);

            messages.Add(new Message(
                recipient,
                method,
                JsonSerializer.SerializeToElement(
                    args[0],
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                )
            ));

            return Task.CompletedTask;
        });

    return proxy;
}

    private static IHubClients CreateClients(List<Message> messages)
    {
        var clients = Substitute.For<IHubClients>();

        clients.All.Returns(_ => CreateProxy("all", messages));

        clients
            .Client(Arg.Any<string>())
            .Returns(call => CreateProxy(
                call.ArgAt<string>(0),
                messages));

        clients
            .Group(Arg.Any<string>())
            .Returns(call => CreateProxy(
                "group:" + call.ArgAt<string>(0),
                messages));

        return clients;
    }

    private static IHubContext<TriviaHub> CreateHub(IHubClients clients)
    {
        var hub = Substitute.For<IHubContext<TriviaHub>>();

        hub.Clients.Returns(clients);

        return hub;
    }

    [Fact]
    public async Task TenRoundsAreScoredByServerAndSavedOnce()
    {
        var clock = new Clock();
        var saved = new List<MatchResult>();
        var results = CreateResults(saved);
        var messages = new List<Message>();
        var clients = CreateClients(messages);

        using var services = new ServiceCollection()
            .AddSingleton<IResultRepository>(results)
            .AddSingleton<IQuestionRepository>(CreateQuestions())
            .BuildServiceProvider();

        var sessions = new TriviaSessions(
            services.GetRequiredService<IServiceScopeFactory>(),
            CreateHub(clients),
            clock,
            new TriviaGameService(new TriviaScoringService()),
            NullLogger<TriviaSessions>.Instance
        );

        var room = new MatchRoom(
            "match",
            [
                new("player-one", "Uno"),
                new("player-two", "Dos")
            ]
        );

        await sessions.JoinAsync(room, "player-one", "c1", default);

        Assert.DoesNotContain(
            messages,
            m => m.Name == "QuestionStarted"
        );

        await sessions.JoinAsync(room, "player-two", "c2", default);

        for (var i = 1; i <= 10; i++)
        {
            var question = messages
                .Last(m => m.Name == "QuestionStarted")
                .Payload;

            Assert.False(
                question.TryGetProperty("correctAnswerId", out _)
            );

            Assert.Equal(
                "q" + i,
                question.GetProperty("questionId").GetString()
            );

            await Assert.ThrowsAsync<HubException>(() =>
                sessions.AnswerAsync(
                    new("match", "q" + i, "a"),
                    "player-one",
                    "intruder",
                    default
                )
            );

            clock.Advance(3);

            await sessions.AnswerAsync(
                new("match", "q" + i, "a"),
                "player-one",
                "c1",
                default
            );

            await Assert.ThrowsAsync<HubException>(() =>
                sessions.AnswerAsync(
                    new("match", "q" + i, "a"),
                    "player-one",
                    "c1",
                    default
                )
            );

            clock.Advance(1);

            await sessions.AnswerAsync(
                new("match", "q" + i, "b"),
                "player-two",
                "c2",
                default
            );

            var review = messages
                .Last(m =>
                    m.Name == "QuestionClosed" &&
                    m.Recipient == "c1")
                .Payload;

            Assert.Equal(
                "a",
                review.GetProperty("correctAnswerId").GetString()
            );

            Assert.Equal(
                220,
                review.GetProperty("pointsEarned").GetInt32()
            );

            clock.Advance(5);
            await sessions.TickAsync(default);
        }

        Assert.Single(saved);
        Assert.Equal("player-one", saved[0].WinnerUserId);
        Assert.Equal(2200, saved[0].Players[0].Score);
        Assert.Equal(0, saved[0].Players[1].Score);

        Assert.Contains(
            messages,
            m => m.Name == "GameFinished"
        );

        await sessions.TickAsync(default);

        Assert.Single(saved);
    }

    [Fact]
    public async Task ReconnectionPreservesAnswerAndTimeoutClosesRound()
    {
        var clock = new Clock();
        var saved = new List<MatchResult>();
        var results = CreateResults(saved);
        var messages = new List<Message>();
        var clients = CreateClients(messages);

        using var services = new ServiceCollection()
            .AddSingleton<IResultRepository>(results)
            .AddSingleton<IQuestionRepository>(CreateQuestions())
            .BuildServiceProvider();

        var sessions = new TriviaSessions(
            services.GetRequiredService<IServiceScopeFactory>(),
            CreateHub(clients),
            clock,
            new TriviaGameService(new TriviaScoringService()),
            NullLogger<TriviaSessions>.Instance
        );

        var room = new MatchRoom(
            "reconnect",
            [
                new("player-one", "Uno"),
                new("player-two", "Dos")
            ]
        );

        await sessions.JoinAsync(room, "player-one", "c1", default);
        await sessions.JoinAsync(room, "player-two", "c2", default);

        clock.Advance(2);

        await sessions.AnswerAsync(
            new("reconnect", "q1", "a"),
            "player-one",
            "c1",
            default
        );

        await sessions.DisconnectAsync("reconnect", "c1");

        await sessions.JoinAsync(
            room,
            "player-one",
            "c3",
            default
        );

        var state = messages
            .Last(m =>
                m.Name == "CurrentState" &&
                m.Recipient == "c3")
            .Payload;

        Assert.True(
            state.GetProperty("currentQuestion")
                .GetProperty("hasAnswered")
                .GetBoolean()
        );

        Assert.Equal(
            "a",
            state.GetProperty("currentQuestion")
                .GetProperty("selectedAnswerId")
                .GetString()
        );

        clock.Advance(13);

        await sessions.TickAsync(default);

        Assert.Contains(
            messages,
            m =>
                m.Name == "QuestionClosed" &&
                m.Recipient == "c2" &&
                m.Payload.GetProperty("pointsEarned").GetInt32() == 0
        );

        await Assert.ThrowsAsync<HubException>(() =>
            sessions.AnswerAsync(
                new("reconnect", "q1", "a"),
                "player-two",
                "c2",
                default
            )
        );

        Assert.Empty(saved);
    }
}
