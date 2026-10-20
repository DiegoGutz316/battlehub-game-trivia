
using System.Collections.Concurrent;
using System.Security.Cryptography;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Services;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace BattleHub.Trivia.Api.Hubs;

public sealed class PracticeSessions(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    TriviaGameService engine,
    IHubContext<PracticeHub> hub)
{
    private sealed class Session(
        GameMatch match,
        string secret,
        DateTimeOffset now)
    {
        public GameMatch Match { get; } = match;

        public string Secret { get; } = secret;

        public string? ConnectionId { get; set; }

        public DateTimeOffset? DisconnectedAt { get; set; }

        public DateTimeOffset? FinishedAt { get; set; }

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public string Phase { get; set; } = "waiting";

        public DateTimeOffset Due { get; set; } =
            now.AddSeconds(10);

        public DateTimeOffset Updated { get; set; } = now;
    }

    private readonly ConcurrentDictionary<string, Session>
        sessions = new();

    private const int MaxSessions = 1000;

    private static string GenerateSecret()
    {
        return Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));
    }

    private static string GenerateMatchId()
    {
        return $"practice-{Guid.NewGuid():N}";
    }

    public async Task<(string MatchId, string Secret)> CreateAsync(
        string displayName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(displayName) ||
            displayName.Length > 50)
        {
            throw new HubException("INVALID_DISPLAY_NAME");
        }

        using var scope = scopes.CreateScope();

        var repository = scope.ServiceProvider
            .GetRequiredService<IQuestionRepository>();

        var questions = await repository.DrawAsync(
            10,
            cancellationToken: ct);

        if (questions.Count != 10 ||
            questions.Any(q => q.Options.Count != 4))
        {
            throw new HubException("INSUFFICIENT_QUESTIONS");
        }

        var matchId = GenerateMatchId();
        var secret = GenerateSecret();
        var now = clock.GetUtcNow();

        var match = engine.CreateMatch(matchId, questions);

        engine.AddPlayer(
            match,
            $"guest-{Guid.NewGuid():N}",
            displayName.Trim());

        var session = new Session(match, secret, now);

        if (sessions.Count >= MaxSessions ||
            !sessions.TryAdd(matchId, session))
        {
            throw new HubException("PRACTICE_UNAVAILABLE");
        }

        return (matchId, secret);
    }

    private Session GetAuthorizedSession(
        string matchId,
        string secret)
    {
        if (string.IsNullOrWhiteSpace(matchId) ||
            string.IsNullOrWhiteSpace(secret) ||
            !sessions.TryGetValue(matchId, out var session))
        {
            throw new HubException("PRACTICE_NOT_FOUND");
        }

        byte[] providedSecret;

        try
        {
            providedSecret = Convert.FromHexString(secret);
        }
        catch (FormatException)
        {
            throw new HubException("PRACTICE_NOT_FOUND");
        }

        var expectedSecret = Convert.FromHexString(
            session.Secret);

        if (providedSecret.Length != expectedSecret.Length ||
            !CryptographicOperations.FixedTimeEquals(
                providedSecret,
                expectedSecret))
        {
            throw new HubException("PRACTICE_NOT_FOUND");
        }

        return session;
    }

    public async Task<object> StartAsync(
        string matchId,
        string secret,
        CancellationToken ct)
    {
        var session = GetAuthorizedSession(matchId, secret);

        await session.Gate.WaitAsync(ct);

        try
        {
            if (session.Phase != "waiting")
            {
                throw new HubException("PRACTICE_ALREADY_STARTED");
            }

            var now = clock.GetUtcNow();

            session.Match.StartedAt = now;

            var question = engine.StartNextQuestion(
                session.Match,
                now);

            session.Phase = "playing";
            session.Due = now.AddSeconds(15);
            session.Updated = now;

            return new
            {
                session.Match.MatchId,
                questionNumber = session.Match.CurrentQuestionIndex + 1,
                totalQuestions = session.Match.Questions.Count,
                question.QuestionId,
                question.Text,
                question.Category,
                options = question.Options.Select(option => new
                {
                    option.AnswerId,
                    option.Text
                }).ToArray(),
                expiresAtUtc = session.Due.UtcDateTime.ToString("O"),
                serverTimeUtc = now.UtcDateTime.ToString("O")
            };
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task<object> AnswerAsync(
        string matchId,
        string secret,
        string connectionId,
        string questionId,
        string answerId,
        CancellationToken ct)
    {
        var session = GetAuthorizedSession(matchId, secret);

        await session.Gate.WaitAsync(ct);

        try
        {
            // Validar que responde la conexión activa.
            if (session.ConnectionId != connectionId)
            {
                throw new HubException("PRACTICE_NOT_CONNECTED");
            }

            var now = clock.GetUtcNow();
            var question = session.Match.CurrentQuestion;

            if (session.Phase != "playing" ||
                question is null ||
                question.QuestionId != questionId)
            {
                throw new HubException("QUESTION_NOT_ACTIVE");
            }

            if (now >= session.Due)
            {
                throw new HubException("QUESTION_CLOSED");
            }

            var player = session.Match.Players.Single();

            if (session.Match.CurrentAnswers.Any(
                answer => answer.UserId == player.UserId))
            {
                throw new HubException("ALREADY_ANSWERED");
            }

            if (!question.Options.Any(
                option => option.AnswerId == answerId))
            {
                throw new HubException("INVALID_ANSWER");
            }

            var points = engine.SubmitAnswer(
                session.Match,
                player.UserId,
                answerId,
                now);

            session.Updated = now;

            return new
            {
                question.QuestionId,
                selectedAnswerId = answerId,
                pointsEarned = points,
                totalScore = player.Score,
                receivedAtUtc = now.UtcDateTime.ToString("O")
            };
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private static object BuildQuestionReview(
        Session session,
        DateTimeOffset now)
    {
        var match = session.Match;
        var question = match.CurrentQuestion!;
        var player = match.Players.Single();

        var answer = match.CurrentAnswers
            .FirstOrDefault(a => a.UserId == player.UserId);

        return new
        {
            question.QuestionId,
            question.CorrectAnswerId,
            selectedAnswerId = answer?.AnswerId,
            isCorrect = answer?.IsCorrect ?? false,
            pointsEarned = answer?.PointsAwarded ?? 0,
            totalScore = player.Score,
            nextQuestionAtUtc = match.IsFinished
                ? null
                : session.Due.UtcDateTime.ToString("O"),
            serverTimeUtc = now.UtcDateTime.ToString("O")
        };
    }

    private object CloseQuestion(
        Session session,
        DateTimeOffset now)
    {
        engine.CloseCurrentQuestion(session.Match, now);

        session.Phase = "reviewing";
        session.Due = now.AddSeconds(5);
        session.Updated = now;

        return BuildQuestionReview(session, now);
    }

    private static object BuildQuestionStarted(
        Session session,
        DateTimeOffset now)
    {
        var match = session.Match;
        var question = match.CurrentQuestion!;

        return new
        {
            match.MatchId,
            questionNumber = match.CurrentQuestionIndex + 1,
            totalQuestions = match.Questions.Count,
            question.QuestionId,
            question.Text,
            question.Category,
            options = question.Options.Select(option => new
            {
                option.AnswerId,
                option.Text
            }).ToArray(),
            expiresAtUtc = session.Due.UtcDateTime.ToString("O"),
            serverTimeUtc = now.UtcDateTime.ToString("O")
        };
    }

    public async Task TickAsync(CancellationToken ct)
    {
        foreach (var session in sessions.Values)
        {
            await session.Gate.WaitAsync(ct);

            try
            {
                var now = clock.GetUtcNow();

                if (now < session.Due)
                {
                    continue;
                }

                object? eventData = null;
                string? eventName = null;

                if (session.Phase == "playing")
                {
                    eventData = CloseQuestion(session, now);
                    eventName = "QuestionClosed";
                }
                else if (session.Phase == "reviewing")
                {
                    if (session.Match.IsFinished)
                    {
                        session.Phase = "finished";
                        session.FinishedAt = now;
                        session.Updated = now;

                        var player = session.Match.Players.Single();

                        eventData = new
                        {
                            session.Match.MatchId,
                            totalQuestions = session.Match.Questions.Count,
                            totalScore = player.Score,
                            correctAnswers = player.CorrectAnswers
                        };

                        eventName = "GameFinished";
                    }
                    else
                    {
                        engine.StartNextQuestion(
                            session.Match,
                            now);

                        session.Phase = "playing";
                        session.Due = now.AddSeconds(15);
                        session.Updated = now;

                        eventData = BuildQuestionStarted(
                            session,
                            now);

                        eventName = "QuestionStarted";
                    }
                }

                if (eventName is not null &&
                    eventData is not null &&
                    session.ConnectionId is not null)
                {
                    await hub.Clients
                        .Client(session.ConnectionId)
                        .SendCoreAsync(
                            eventName,
                            new[] { eventData },
                            ct);
                }
            }
            finally
            {
                session.Gate.Release();
            }
        }
    }

    public async Task AttachConnectionAsync(
        string matchId,
        string secret,
        string connectionId,
        CancellationToken ct)
    {
        var session = GetAuthorizedSession(matchId, secret);

        await session.Gate.WaitAsync(ct);

        try
        {
            if (session.ConnectionId is not null &&
                session.ConnectionId != connectionId)
            {
                throw new HubException("PRACTICE_ALREADY_CONNECTED");
            }

            session.ConnectionId = connectionId;
            session.DisconnectedAt = null;
            session.Updated = clock.GetUtcNow();
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task DetachConnectionAsync(
        string matchId,
        string connectionId)
    {
        if (!sessions.TryGetValue(matchId, out var session))
        {
            return;
        }

        await session.Gate.WaitAsync();

        try
        {
            if (session.ConnectionId == connectionId)
            {
                session.ConnectionId = null;
                session.DisconnectedAt = clock.GetUtcNow();
                session.Updated = clock.GetUtcNow();
            }
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task<object> GetStateAsync(
        string matchId,
        string secret,
        CancellationToken ct)
    {
        var session = GetAuthorizedSession(matchId, secret);

        await session.Gate.WaitAsync(ct);

        try
        {
            var now = clock.GetUtcNow();
            var match = session.Match;
            var player = match.Players.Single();

            object? currentQuestion = null;
            object? review = null;

            if (session.Phase == "playing")
            {
                currentQuestion = BuildQuestionStarted(
                    session,
                    now);
            }
            else if (session.Phase == "reviewing")
            {
                review = BuildQuestionReview(
                    session,
                    now);
            }

            return new
            {
                match.MatchId,
                phase = session.Phase,
                questionNumber = match.CurrentQuestionIndex + 1,
                totalQuestions = match.Questions.Count,
                totalScore = player.Score,
                correctAnswers = player.CorrectAnswers,
                currentQuestion,
                review,
                serverTimeUtc = now.UtcDateTime.ToString("O")
            };
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task CleanupExpiredAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        foreach (var entry in sessions)
        {
            var session = entry.Value;

            await session.Gate.WaitAsync(ct);

            try
            {
                var disconnectedTooLong =
                    session.DisconnectedAt is not null &&
                    now - session.DisconnectedAt.Value >=
                        TimeSpan.FromMinutes(5);

                var finishedTooLong =
                    session.FinishedAt is not null &&
                    now - session.FinishedAt.Value >=
                        TimeSpan.FromMinutes(10);

                if (disconnectedTooLong || finishedTooLong)
                {
                    sessions.TryRemove(
                        new KeyValuePair<string, Session>(
                            entry.Key,
                            session));
                }
            }
            finally
            {
                session.Gate.Release();
            }
        }
    }
}
