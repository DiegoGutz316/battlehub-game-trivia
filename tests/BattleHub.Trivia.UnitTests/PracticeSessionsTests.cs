
using System.Text.Json;
using BattleHub.Trivia.Api.Hubs;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Interfaces;
using BattleHub.Trivia.Domain.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace BattleHub.Trivia.UnitTests;

public sealed class PracticeSessionsTests
{
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now =
            new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(int seconds)
        {
            now = now.AddSeconds(seconds);
        }
    }

    private static IQuestionRepository CreateQuestions()
    {
        var repository = Substitute.For<IQuestionRepository>();

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

        repository
            .DrawAsync(
                Arg.Any<int>(),
                Arg.Any<QuestionFilter?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(bank));

        return repository;
    }

    private static IHubContext<PracticeHub> CreateHub()
    {
        var hub = Substitute.For<IHubContext<PracticeHub>>();

        var clients = Substitute.For<IHubClients>();

        var proxy = Substitute.For<ISingleClientProxy>();

        proxy
            .SendCoreAsync(
                Arg.Any<string>(),
                Arg.Any<object?[]>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        clients
            .Client(Arg.Any<string>())
            .Returns(proxy);

        hub.Clients.Returns(clients);

        return hub;
    }

    private static (
        PracticeSessions Sessions,
        ServiceProvider Services,
        Clock Clock)
        CreateSessions()
    {
        var clock = new Clock();

        var services = new ServiceCollection()
            .AddSingleton<IQuestionRepository>(CreateQuestions())
            .BuildServiceProvider();

        var sessions = new PracticeSessions(
            services.GetRequiredService<IServiceScopeFactory>(),
            clock,
            new TriviaGameService(new TriviaScoringService()),
            CreateHub());

        return (sessions, services, clock);
    }

    [Fact]
    public async Task PracticeStartsWithTenQuestions()
    {
        var (sessions, services, _) = CreateSessions();

        using (services)
        {
            var (matchId, secret) =
                await sessions.CreateAsync("Jugador", default);

            await sessions.AttachConnectionAsync(
                matchId,
                secret,
                "connection-1",
                default);

            var firstQuestion =
                await sessions.StartAsync(matchId, secret, default);

            var json = JsonSerializer.SerializeToElement(
                firstQuestion,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            Assert.Equal(
                10,
                json.GetProperty("totalQuestions").GetInt32());

            Assert.Equal(
                1,
                json.GetProperty("questionNumber").GetInt32());

            Assert.Equal(
                4,
                json.GetProperty("options").GetArrayLength());

            Assert.False(
                json.TryGetProperty("correctAnswerId", out _));
        }
    }

    [Fact]
    public async Task EmptyDisplayNameIsRejected()
    {
        var (sessions, services, _) = CreateSessions();

        using (services)
        {
            await Assert.ThrowsAsync<HubException>(() =>
                sessions.CreateAsync("   ", default));
        }
    }

    [Fact]
    public async Task AnotherConnectionCannotAnswer()
    {
        var (sessions, services, _) = CreateSessions();

        using (services)
        {
            var (matchId, secret) =
                await sessions.CreateAsync("Jugador", default);

            await sessions.AttachConnectionAsync(
                matchId,
                secret,
                "connection-1",
                default);

            await sessions.StartAsync(
                matchId,
                secret,
                default);

            var exception = await Assert.ThrowsAsync<HubException>(
                () => sessions.AnswerAsync(
                    matchId,
                    secret,
                    "connection-intruder",
                    "q1",
                    "a",
                    default));

            Assert.Equal(
                "PRACTICE_NOT_CONNECTED",
                exception.Message);
        }
    }

    
[Fact]
public async Task QuestionClosesAfterFifteenSeconds()
{
    var (sessions, services, clock) = CreateSessions();

    using (services)
    {
        var (matchId, secret) =
            await sessions.CreateAsync("Jugador", default);

        await sessions.AttachConnectionAsync(
            matchId,
            secret,
            "connection-1",
            default);

        await sessions.StartAsync(
            matchId,
            secret,
            default);

        // Avanzamos 14 segundos.
        clock.Advance(14);

        await sessions.TickAsync(default);

        var stateBefore = await sessions.GetStateAsync(
            matchId,
            secret,
            default);

        var beforeJson = JsonSerializer.SerializeToElement(
            stateBefore,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            "playing",
            beforeJson.GetProperty("phase").GetString());

        // Se cumple el segundo 15.
        clock.Advance(1);

        await sessions.TickAsync(default);

        var stateAfter = await sessions.GetStateAsync(
            matchId,
            secret,
            default);

        var afterJson = JsonSerializer.SerializeToElement(
            stateAfter,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            "reviewing",
            afterJson.GetProperty("phase").GetString());

        // Ya no se permite responder.
        var exception = await Assert.ThrowsAsync<HubException>(
            () => sessions.AnswerAsync(
                matchId,
                secret,
                "connection-1",
                "q1",
                "a",
                default));

        Assert.Equal(
            "QUESTION_NOT_ACTIVE",
            exception.Message);
    }
}


[Fact]
public async Task PlayerCannotAnswerSameQuestionTwice()
{
    var (sessions, services, clock) = CreateSessions();

    using (services)
    {
        var (matchId, secret) =
            await sessions.CreateAsync("Jugador", default);

        await sessions.AttachConnectionAsync(
            matchId,
            secret,
            "connection-1",
            default);

        await sessions.StartAsync(
            matchId,
            secret,
            default);

        // El jugador responde después de 3 segundos.
        clock.Advance(3);

        var firstAnswer = await sessions.AnswerAsync(
            matchId,
            secret,
            "connection-1",
            "q1",
            "a",
            default);

        Assert.NotNull(firstAnswer);

        // Intenta responder nuevamente la misma pregunta.
        var exception = await Assert.ThrowsAsync<HubException>(
            () => sessions.AnswerAsync(
                matchId,
                secret,
                "connection-1",
                "q1",
                "b",
                default));

        Assert.Equal(
            "ALREADY_ANSWERED",
            exception.Message);
    }
}


[Fact]
public async Task ReconnectionPreservesPracticeProgress()
{
    var (sessions, services, clock) = CreateSessions();

    using (services)
    {
        var (matchId, secret) =
            await sessions.CreateAsync("Jugador", default);

        await sessions.AttachConnectionAsync(
            matchId,
            secret,
            "connection-1",
            default);

        await sessions.StartAsync(
            matchId,
            secret,
            default);

        // El jugador responde la primera pregunta.
        clock.Advance(3);

        await sessions.AnswerAsync(
            matchId,
            secret,
            "connection-1",
            "q1",
            "a",
            default);

        // Guardamos el estado antes de desconectarse.
        var stateBefore = await sessions.GetStateAsync(
            matchId,
            secret,
            default);

        var beforeJson = JsonSerializer.SerializeToElement(
            stateBefore,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var scoreBefore =
            beforeJson.GetProperty("totalScore").GetInt32();

        // Se pierde la conexión original.
        await sessions.DetachConnectionAsync(
            matchId,
            "connection-1");

        // El jugador se reconecta con una conexión nueva.
        await sessions.AttachConnectionAsync(
            matchId,
            secret,
            "connection-2",
            default);

        var stateAfter = await sessions.GetStateAsync(
            matchId,
            secret,
            default);

        var afterJson = JsonSerializer.SerializeToElement(
            stateAfter,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // La puntuación se conserva.
        Assert.Equal(
            scoreBefore,
            afterJson.GetProperty("totalScore").GetInt32());

        // Sigue en la misma pregunta.
        Assert.Equal(
            1,
            afterJson.GetProperty("questionNumber").GetInt32());

        Assert.Equal(
            "playing",
            afterJson.GetProperty("phase").GetString());

        // No puede volver a responder una pregunta ya contestada.
        var exception = await Assert.ThrowsAsync<HubException>(
            () => sessions.AnswerAsync(
                matchId,
                secret,
                "connection-2",
                "q1",
                "b",
                default));

        Assert.Equal(
            "ALREADY_ANSWERED",
            exception.Message);
    }
}


[Fact]
public async Task PracticeFinishesAfterTenQuestions()
{
    var (sessions, services, clock) = CreateSessions();

    using (services)
    {
        var (matchId, secret) =
            await sessions.CreateAsync("Jugador", default);

        await sessions.AttachConnectionAsync(
            matchId,
            secret,
            "connection-1",
            default);

        await sessions.StartAsync(
            matchId,
            secret,
            default);

        for (var i = 1; i <= 10; i++)
        {
            // El jugador responde correctamente.
            clock.Advance(3);

            await sessions.AnswerAsync(
                matchId,
                secret,
                "connection-1",
                "q" + i,
                "a",
                default);

            // Se completan los 15 segundos de la pregunta.
            clock.Advance(12);
            await sessions.TickAsync(default);

            // Se completan los 5 segundos de revisión.
            clock.Advance(5);
            await sessions.TickAsync(default);
        }

        var state = await sessions.GetStateAsync(
            matchId,
            secret,
            default);

        var json = JsonSerializer.SerializeToElement(
            state,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            "finished",
            json.GetProperty("phase").GetString());

        Assert.Equal(
            10,
            json.GetProperty("totalQuestions").GetInt32());

        Assert.Equal(
            10,
            json.GetProperty("correctAnswers").GetInt32());

        Assert.True(
            json.GetProperty("totalScore").GetInt32() > 0);
    }
}


[Fact]
public async Task DisconnectedPracticeIsRemovedAfterFiveMinutes()
{
    var (sessions, services, clock) = CreateSessions();

    using (services)
    {
        var (matchId, secret) =
            await sessions.CreateAsync("Jugador", default);

        await sessions.AttachConnectionAsync(
            matchId,
            secret,
            "connection-1",
            default);

        await sessions.StartAsync(
            matchId,
            secret,
            default);

        // Simulamos que el jugador pierde la conexión.
        await sessions.DetachConnectionAsync(
            matchId,
            "connection-1");

        // Después de 4 minutos, la sesión debe existir.
        clock.Advance(240);

        await sessions.CleanupExpiredAsync(default);

        var state = await sessions.GetStateAsync(
            matchId,
            secret,
            default);

        Assert.NotNull(state);

        // Avanzamos un minuto más: 5 minutos en total.
        clock.Advance(60);

        await sessions.CleanupExpiredAsync(default);

        // La sesión ya no debe estar disponible.
        var exception = await Assert.ThrowsAsync<HubException>(
            () => sessions.GetStateAsync(
                matchId,
                secret,
                default));

        Assert.Equal(
            "PRACTICE_NOT_FOUND",
            exception.Message);
    }
}


[Fact]
public async Task FinishedPracticeIsRemovedAfterTenMinutes()
{
    var (sessions, services, clock) = CreateSessions();

    using (services)
    {
        var (matchId, secret) =
            await sessions.CreateAsync("Jugador", default);

        await sessions.AttachConnectionAsync(
            matchId,
            secret,
            "connection-1",
            default);

        await sessions.StartAsync(
            matchId,
            secret,
            default);

        // Completamos las 10 preguntas.
        for (var i = 1; i <= 10; i++)
        {
            clock.Advance(3);

            await sessions.AnswerAsync(
                matchId,
                secret,
                "connection-1",
                "q" + i,
                "a",
                default);

            clock.Advance(12);
            await sessions.TickAsync(default);

            clock.Advance(5);
            await sessions.TickAsync(default);
        }

        // Confirmamos que la partida terminó.
        var finishedState = await sessions.GetStateAsync(
            matchId,
            secret,
            default);

        var finishedJson = JsonSerializer.SerializeToElement(
            finishedState,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(
            "finished",
            finishedJson.GetProperty("phase").GetString());

        // A los 9 minutos todavía debe existir.
        clock.Advance(540);

        await sessions.CleanupExpiredAsync(default);

        var stateBeforeCleanup = await sessions.GetStateAsync(
            matchId,
            secret,
            default);

        Assert.NotNull(stateBeforeCleanup);

        // Al completar 10 minutos debe eliminarse.
        clock.Advance(60);

        await sessions.CleanupExpiredAsync(default);

        var exception = await Assert.ThrowsAsync<HubException>(
            () => sessions.GetStateAsync(
                matchId,
                secret,
                default));

        Assert.Equal(
            "PRACTICE_NOT_FOUND",
            exception.Message);
    }
}

}
