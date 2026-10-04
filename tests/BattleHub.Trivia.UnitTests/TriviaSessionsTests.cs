using System.Text.Json;
using BattleHub.Trivia.Api.Hubs;
using BattleHub.Trivia.Api.Matchmaking;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Interfaces;
using BattleHub.Trivia.Domain.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BattleHub.Trivia.UnitTests;

public sealed class TriviaSessionsTests
{
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(int seconds) => now = now.AddSeconds(seconds);
    }
    private sealed class Results : IResultRepository
    {
        public List<MatchResult> Saved { get; } = [];
        public Task SaveAsync(MatchResult result, CancellationToken ct = default) { Saved.Add(result); return Task.CompletedTask; }
        public Task<MatchResult?> GetByMatchIdAsync(string id, CancellationToken ct = default) => Task.FromResult(Saved.FirstOrDefault(r => r.MatchId == id));
        public Task<IReadOnlyList<MatchResult>> GetHistoryByUserIdAsync(string id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MatchResult>>(Saved);
        public Task<PlayerStats> GetStatsByUserIdAsync(string id, CancellationToken ct = default) => Task.FromResult(new PlayerStats { UserId = id });
    }
    private sealed class Questions : IQuestionRepository
    {
        public IReadOnlyList<Question> Bank { get; } = Enumerable.Range(1, 10).Select(i => new Question {
            QuestionId = "q" + i, Text = "Pregunta " + i, Category = "General", CorrectAnswerId = "a",
            Options = new[] { "a", "b", "c", "d" }.Select(id => new AnswerOption { AnswerId = id, Text = id }).ToArray()
        }).ToArray();
        public Task<Question?> GetByIdAsync(string id, CancellationToken ct = default) => Task.FromResult(Bank.FirstOrDefault(q => q.QuestionId == id));
        public Task<IReadOnlyList<Question>> DrawAsync(int count, QuestionFilter? filter = null, CancellationToken cancellationToken = default) => Task.FromResult(Bank);
    }
    private sealed record Message(string Recipient, string Name, JsonElement Payload);
    private sealed class Proxy(string recipient, List<Message> messages) : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default)
        { messages.Add(new Message(recipient, method, JsonSerializer.SerializeToElement(args[0], new JsonSerializerOptions(JsonSerializerDefaults.Web)))); return Task.CompletedTask; }
    }
    private sealed class RecordingClients : IHubClients
    {
        public List<Message> Messages { get; } = [];
        public IClientProxy All => new Proxy("all", Messages);
        public IClientProxy AllExcept(IReadOnlyList<string> ids) => All;
        public IClientProxy Client(string id) => new Proxy(id, Messages);
        public IClientProxy Clients(IReadOnlyList<string> ids) => All;
        public IClientProxy Group(string id) => new Proxy("group:" + id, Messages);
        public IClientProxy GroupExcept(string id, IReadOnlyList<string> excluded) => Group(id);
        public IClientProxy Groups(IReadOnlyList<string> ids) => All;
        public IClientProxy User(string id) => Client(id);
        public IClientProxy Users(IReadOnlyList<string> ids) => All;
    }
    private sealed class Hub(RecordingClients clients) : IHubContext<TriviaHub>
    {
        public IHubClients Clients => clients;
        public IGroupManager Groups => throw new NotSupportedException();
    }
    [Fact]
    public async Task TenRoundsAreScoredByServerAndSavedOnce()
    {
        var clock = new Clock(); var results = new Results(); var clients = new RecordingClients();
        using var services = new ServiceCollection().AddSingleton<IResultRepository>(results).AddSingleton<IQuestionRepository>(new Questions()).BuildServiceProvider();
        var sessions = new TriviaSessions(services.GetRequiredService<IServiceScopeFactory>(), new Hub(clients), clock,
            new TriviaGameService(new TriviaScoringService()), NullLogger<TriviaSessions>.Instance);
        var room = new MatchRoom("match", "trivia", "Started", [new("auth0|one", "Uno"), new("auth0|two", "Dos")]);
        await sessions.JoinAsync(room, "auth0|one", "c1", default);
        Assert.DoesNotContain(clients.Messages, m => m.Name == "QuestionStarted");
        await sessions.JoinAsync(room, "auth0|two", "c2", default);
        for (var i = 1; i <= 10; i++)
        {
            var question = clients.Messages.Last(m => m.Name == "QuestionStarted").Payload;
            Assert.False(question.TryGetProperty("correctAnswerId", out _));
            Assert.Equal("q" + i, question.GetProperty("questionId").GetString());
            await Assert.ThrowsAsync<HubException>(() => sessions.AnswerAsync(new("match", "q" + i, "a"), "auth0|one", "intruder", default));
            clock.Advance(3);
            await sessions.AnswerAsync(new("match", "q" + i, "a"), "auth0|one", "c1", default);
            await Assert.ThrowsAsync<HubException>(() => sessions.AnswerAsync(new("match", "q" + i, "a"), "auth0|one", "c1", default));
            clock.Advance(1);
            await sessions.AnswerAsync(new("match", "q" + i, "b"), "auth0|two", "c2", default);
            var review = clients.Messages.Last(m => m.Name == "QuestionClosed" && m.Recipient == "c1").Payload;
            Assert.Equal("a", review.GetProperty("correctAnswerId").GetString());
            Assert.Equal(220, review.GetProperty("pointsEarned").GetInt32());
            clock.Advance(5);
            await sessions.TickAsync(default);
        }
        Assert.Single(results.Saved);
        Assert.Equal("auth0|one", results.Saved[0].WinnerUserId);
        Assert.Equal(2200, results.Saved[0].Players[0].Score);
        Assert.Equal(0, results.Saved[0].Players[1].Score);
        Assert.Contains(clients.Messages, m => m.Name == "GameFinished");
        await sessions.TickAsync(default);
        Assert.Single(results.Saved);
    }
    [Fact]
    public async Task ReconnectionPreservesAnswerAndTimeoutClosesRound()
    {
        var clock = new Clock(); var results = new Results(); var clients = new RecordingClients();
        using var services = new ServiceCollection().AddSingleton<IResultRepository>(results).AddSingleton<IQuestionRepository>(new Questions()).BuildServiceProvider();
        var sessions = new TriviaSessions(services.GetRequiredService<IServiceScopeFactory>(), new Hub(clients), clock,
            new TriviaGameService(new TriviaScoringService()), NullLogger<TriviaSessions>.Instance);
        var room = new MatchRoom("reconnect", "trivia", "Started", [new("auth0|one", "Uno"), new("auth0|two", "Dos")]);
        await sessions.JoinAsync(room, "auth0|one", "c1", default);
        await sessions.JoinAsync(room, "auth0|two", "c2", default);
        clock.Advance(2);
        await sessions.AnswerAsync(new("reconnect", "q1", "a"), "auth0|one", "c1", default);
        await sessions.DisconnectAsync("reconnect", "c1");
        await sessions.JoinAsync(room, "auth0|one", "c3", default);
        var state = clients.Messages.Last(m => m.Name == "CurrentState" && m.Recipient == "c3").Payload;
        Assert.True(state.GetProperty("currentQuestion").GetProperty("hasAnswered").GetBoolean());
        Assert.Equal("a", state.GetProperty("currentQuestion").GetProperty("selectedAnswerId").GetString());
        clock.Advance(13);
        await sessions.TickAsync(default);
        Assert.Contains(clients.Messages, m => m.Name == "QuestionClosed" && m.Recipient == "c2" && m.Payload.GetProperty("pointsEarned").GetInt32() == 0);
        await Assert.ThrowsAsync<HubException>(() => sessions.AnswerAsync(new("reconnect", "q1", "a"), "auth0|two", "c2", default));
        Assert.Empty(results.Saved);
    }
}

