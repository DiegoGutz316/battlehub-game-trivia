using System.Collections.Concurrent;
using BattleHub.Trivia.Api.Matchmaking;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Exceptions;
using BattleHub.Trivia.Domain.Interfaces;
using BattleHub.Trivia.Domain.Services;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Trivia.Api.Hubs;

public sealed class TriviaSessions(IServiceScopeFactory scopes, IHubContext<TriviaHub> hub, TimeProvider clock,
    TriviaGameService engine, ILogger<TriviaSessions> logger)
{
    private sealed class Session(GameMatch match, DateTimeOffset now)
    {
        public GameMatch Match { get; } = match;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public Dictionary<string, string> Connections { get; } = [];
        public string Phase = "waiting";
        public DateTimeOffset Due = now.AddSeconds(10);
        public DateTimeOffset Updated = now;
        public bool Saved;
        public HashSet<string> Joined { get; } = [];
        public Dictionary<string, object> Reviews { get; } = [];
        public Dictionary<(string User, string Category), (int Correct, int Incorrect)> Categories { get; } = [];
        public object? Final;
    }
    private readonly ConcurrentDictionary<string, Session> matches = new();
    private readonly SemaphoreSlim creation = new(1, 1);
    private static string Utc(DateTimeOffset value) => value.UtcDateTime.ToString("O");
    private object[] Players(Session session) => session.Match.Players.OrderByDescending(p => p.Score).ThenBy(p => p.CorrectAnswersTimeMs)
        .Select(p => (object)new { p.UserId, p.DisplayName, p.Score, isConnected = session.Connections.Values.Contains(p.UserId) }).ToArray();
    private object? Question(Session session, string? user = null)
    {
        var match = session.Match;
        if (session.Phase != "playing" || match.CurrentQuestion is null) return null;
        var q = match.CurrentQuestion;
        var answer = match.CurrentAnswers.FirstOrDefault(a => a.UserId == user);
        return new { q.QuestionId, questionNumber = match.CurrentQuestionIndex + 1, totalQuestions = match.Questions.Count,
            q.Text, q.Category, options = q.Options.Select(o => new { o.AnswerId, o.Text }), expiresAtUtc = Utc(session.Due),
            hasAnswered = answer is not null, selectedAnswerId = answer?.AnswerId, serverTimeUtc = Utc(clock.GetUtcNow()) };
    }
    private Task State(Session session, string user, string connection) => hub.Clients.Client(connection).SendAsync("CurrentState", new
    {
        session.Match.MatchId, phase = session.Phase, serverTimeUtc = Utc(clock.GetUtcNow()),
        totalQuestions = session.Match.Questions.Count, players = Players(session), currentQuestion = Question(session, user),
        lastQuestionResult = session.Phase == "reviewing" ? session.Reviews.GetValueOrDefault(user) : null
    });
    public async Task JoinAsync(MatchRoom room, string user, string connection, CancellationToken ct)
    {
        await creation.WaitAsync(ct);
        Session session;
        try
        {
            if (!matches.TryGetValue(room.Id, out session!))
            {
                if (matches.Count >= 1000) throw new HubException("Servicio ocupado. Reintenta.");
                using var scope = scopes.CreateScope();
                if (await scope.ServiceProvider.GetRequiredService<IResultRepository>().GetByMatchIdAsync(room.Id, ct) is not null)
                    throw new HubException("MATCH_FINISHED");
                var questions = await scope.ServiceProvider.GetRequiredService<IQuestionRepository>().DrawAsync(10, cancellationToken: ct);
                if (questions.Count != 10 || questions.Any(q => q.Options.Count != 4)) throw new HubException("El banco necesita al menos diez preguntas con cuatro opciones.");
                var match = engine.CreateMatch(room.Id, questions);
                foreach (var player in room.Participants) engine.AddPlayer(match, player.UserId, player.DisplayName);
                session = new Session(match, clock.GetUtcNow());
                matches[room.Id] = session;
            }
        }
        finally { creation.Release(); }
        await session.Gate.WaitAsync(ct);
        try
        {
            if (!session.Match.Players.Any(p => p.UserId == user)) throw new HubException("PLAYER_NOT_IN_MATCH");
            var wasConnected = !session.Joined.Add(user);
            session.Connections[connection] = user;
            var player = session.Match.Players.First(p => p.UserId == user);
            await hub.Clients.Group(room.Id).SendAsync(wasConnected ? "PlayerReconnected" : "PlayerJoined", new { player.UserId, player.DisplayName }, ct);
            if (session.Phase == "waiting" && session.Connections.Values.Distinct().Count() == session.Match.Players.Count) await StartQuestion(session, ct);
            await State(session, user, connection);
            if (session.Final is not null) await hub.Clients.Client(connection).SendAsync("GameFinished", session.Final, ct);
        }
        finally { session.Gate.Release(); }
    }
    private async Task StartQuestion(Session session, CancellationToken ct)
    {
        var first = session.Phase == "waiting";
        var now = clock.GetUtcNow();
        if (first)
        {
            session.Match.StartedAt = now;
            await hub.Clients.Group(session.Match.MatchId).SendAsync("GameStarted", new { session.Match.MatchId,
                totalQuestions = session.Match.Questions.Count, startedAtUtc = Utc(now), serverTimeUtc = Utc(now) }, ct);
        }
        engine.StartNextQuestion(session.Match, now);
        session.Phase = "playing";
        session.Due = now.AddSeconds(15);
        session.Updated = now;
        await hub.Clients.Group(session.Match.MatchId).SendAsync("QuestionStarted", Question(session), ct);
    }
    public async Task AnswerAsync(AnswerRequest request, string user, string connection, CancellationToken ct)
    {
        if (!matches.TryGetValue(request.MatchId, out var session)) throw new HubException("MATCH_NOT_FOUND");
        await session.Gate.WaitAsync(ct);
        try
        {
            if (!session.Connections.TryGetValue(connection, out var connectedUser) || connectedUser != user) throw new HubException("PLAYER_NOT_IN_MATCH");
            var now = clock.GetUtcNow();
            if (session.Phase != "playing" || session.Match.CurrentQuestion?.QuestionId != request.QuestionId) throw new HubException("QUESTION_NOT_ACTIVE");
            if (now >= session.Due) { await CloseQuestion(session, ct); throw new HubException("QUESTION_CLOSED"); }
            if (session.Match.CurrentAnswers.Any(a => a.UserId == user)) throw new HubException("ALREADY_ANSWERED");
            if (!session.Match.CurrentQuestion.Options.Any(a => a.AnswerId == request.AnswerId)) throw new HubException("INVALID_ANSWER");
            engine.SubmitAnswer(session.Match, user, request.AnswerId, now);
            await hub.Clients.Client(connection).SendAsync("AnswerReceived", new { request.QuestionId, request.AnswerId, receivedAtUtc = Utc(now) }, ct);
            var connected = session.Connections.Values.Distinct().ToArray();
            if (connected.Length > 0 && connected.All(id => session.Match.CurrentAnswers.Any(a => a.UserId == id))) await CloseQuestion(session, ct);
        }
        finally { session.Gate.Release(); }
    }
    private async Task CloseQuestion(Session session, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var question = session.Match.CurrentQuestion!;
        engine.CloseCurrentQuestion(session.Match, now);
        session.Phase = "reviewing";
        session.Due = now.AddSeconds(5);
        session.Updated = now;
        session.Reviews.Clear();
        foreach (var player in session.Match.Players)
        {
            var answer = session.Match.CurrentAnswers.FirstOrDefault(a => a.UserId == player.UserId);
            var key = (player.UserId, question.Category ?? "Sin categoría");
            var counts = session.Categories.GetValueOrDefault(key);
            session.Categories[key] = (counts.Correct + (answer?.IsCorrect == true ? 1 : 0), counts.Incorrect + (answer?.IsCorrect == true ? 0 : 1));
            var review = new { question.QuestionId, question.CorrectAnswerId, selectedAnswerId = answer?.AnswerId,
                isCorrect = answer?.IsCorrect ?? false, pointsEarned = answer?.PointsAwarded ?? 0, totalScore = player.Score,
                nextQuestionAtUtc = session.Match.IsFinished ? null : Utc(session.Due), serverTimeUtc = Utc(now) };
            session.Reviews[player.UserId] = review;
            foreach (var conn in session.Connections.Where(p => p.Value == player.UserId)) await hub.Clients.Client(conn.Key).SendAsync("QuestionClosed", review, ct);
        }
        await hub.Clients.Group(session.Match.MatchId).SendAsync("ScoreboardUpdated", new { players = Players(session) }, ct);
    }
    private async Task Finish(Session session, CancellationToken ct)
    {
        var match = session.Match;
        var ordered = match.Players.OrderByDescending(p => p.Score).ThenBy(p => p.CorrectAnswersTimeMs).ToArray();
        var players = ordered.Select(p => new PlayerResult
        {
            UserId = p.UserId, DisplayName = p.DisplayName, Score = p.Score, CorrectAnswers = p.CorrectAnswers,
            CorrectAnswersTimeMs = p.CorrectAnswersTimeMs,
            Position = 1 + ordered.Count(o => o.Score > p.Score || (o.Score == p.Score && o.CorrectAnswersTimeMs < p.CorrectAnswersTimeMs))
        }).ToArray();
        var winner = engine.GetWinner(match);
        var result = new MatchResult { MatchId = match.MatchId, StartedAt = match.StartedAt,
            FinishedAt = match.FinishedAt!.Value, Players = players, WinnerUserId = winner?.UserId, IsTie = winner is null,
            DecidedByTiebreak = winner is not null && ordered.Count(p => p.Score == winner.Score) > 1,
            Metadata = new TriviaMetadata { CategoryScores = session.Categories.Select(pair => new CategoryScore {
                UserId = pair.Key.User, Category = pair.Key.Category, CorrectAnswers = pair.Value.Correct, IncorrectAnswers = pair.Value.Incorrect
            }).ToArray() } };
        if (!session.Saved)
        {
            using var scope = scopes.CreateScope();
            try { await scope.ServiceProvider.GetRequiredService<IResultRepository>().SaveAsync(result, ct); }
            catch (MatchAlreadyExistsException) { }
            session.Saved = true;
        }
        session.Phase = "finished";
        session.Updated = clock.GetUtcNow();
        session.Final = new { result.MatchId, result.Players, result.WinnerUserId, result.IsTie, result.DecidedByTiebreak, finishedAtUtc = Utc(result.FinishedAt) };
        await hub.Clients.Group(match.MatchId).SendAsync("GameFinished", session.Final, ct);
    }
    public async Task DisconnectAsync(string id, string connection)
    {
        if (!matches.TryGetValue(id, out var session)) return;
        await session.Gate.WaitAsync();
        try
        {
            if (!session.Connections.Remove(connection, out var user)) return;
            if (!session.Connections.Values.Contains(user))
            {
                var player = session.Match.Players.First(p => p.UserId == user);
                await hub.Clients.Group(id).SendAsync("PlayerDisconnected", new { player.UserId, player.DisplayName });
            }
        }
        finally { session.Gate.Release(); }
    }
    public async Task TickAsync(CancellationToken ct)
    {
        foreach (var pair in matches)
        {
            var session = pair.Value;
            if (!await session.Gate.WaitAsync(0, ct)) continue;
            try
            {
                var now = clock.GetUtcNow();
                if (session.Phase == "finished")
                {
                    if (now - session.Updated > TimeSpan.FromMinutes(10)) matches.TryRemove(pair.Key, out _);
                }
                else if (now >= session.Due)
                {
                    if (session.Phase == "playing") await CloseQuestion(session, ct);
                    else if (session.Match.IsFinished) await Finish(session, ct);
                    else await StartQuestion(session, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception) { session.Due = clock.GetUtcNow().AddSeconds(5); logger.LogError("No se pudo avanzar o guardar la partida {MatchId}; se reintentará.", pair.Key); }
            finally { session.Gate.Release(); }
        }
    }
}

public sealed class TriviaWorker(TriviaSessions sessions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        while (await timer.WaitForNextTickAsync(stoppingToken)) await sessions.TickAsync(stoppingToken);
    }
}
