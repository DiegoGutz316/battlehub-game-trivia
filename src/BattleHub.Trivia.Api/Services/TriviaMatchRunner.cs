using System.Collections.Concurrent;
using BattleHub.Trivia.Domain.Interfaces;
using BattleHub.Trivia.Domain.Services;
using Microsoft.AspNetCore.SignalR;
using BattleHub.Trivia.Api.Hubs;

namespace BattleHub.Trivia.Api.Services;

public sealed class TriviaMatchRunner
{
    private readonly IGameRepository _gameRepository;
    private readonly TriviaGameService _gameService;
    private readonly IHubContext<TriviaHub> _hubContext;

    private readonly ConcurrentDictionary<string, byte> _runningMatches = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _questionCompletions = new();

    public TriviaMatchRunner(
        IGameRepository gameRepository,
        TriviaGameService gameService,
        IHubContext<TriviaHub> hubContext)
    {
        _gameRepository = gameRepository;
        _gameService = gameService;
        _hubContext = hubContext;
    }

    public Task StartAsync(string matchId)
    {
        if (!_runningMatches.TryAdd(matchId, 0))
        {
            return Task.CompletedTask;
        }

        _ = RunMatchAsync(matchId);

        return Task.CompletedTask;
    }

    public void NotifyAnswerReceived(string matchId)
    {
        var match = _gameRepository.Get(matchId);

        if (match is null ||
            match.IsFinished ||
            match.CurrentQuestion is null)
        {
            return;
        }

        if (match.Players.Count == 0)
        {
            return;
        }

        var answeredPlayers = match.CurrentAnswers
            .Select(answer => answer.UserId)
            .Distinct()
            .Count();

        if (answeredPlayers >= match.Players.Count &&
            _questionCompletions.TryGetValue(
                matchId,
                out var completion))
        {
            completion.TrySetResult();
        }
    }

    private async Task RunMatchAsync(string matchId)
{
    try
    {
        var match = _gameRepository.Get(matchId);

        if (match is null || match.IsFinished)
        {
            return;
        }

        var gameStartedAt = DateTimeOffset.UtcNow;

await _hubContext.Clients
    .Group(matchId)
    .SendAsync(
        "GameStarted",
        new
        {
            matchId = match.MatchId,
            totalQuestions = match.Questions.Count,
            startedAtUtc = gameStartedAt,
            serverTimeUtc = DateTimeOffset.UtcNow
        });

        while (!match.IsFinished)
        {
            var startedAt = DateTimeOffset.UtcNow;

            var question = _gameService.StartNextQuestion(
                match,
                startedAt);

            var expiresAt = startedAt.AddSeconds(
                TriviaScoringService.QuestionTimeSeconds);

            await _hubContext.Clients
                .Group(matchId)
                .SendAsync(
                    "QuestionStarted",
                    new
                    {
                        questionId = question.QuestionId,
                        questionNumber = match.CurrentQuestionIndex + 1,
                        totalQuestions = match.Questions.Count,
                        text = question.Text,
                        category = question.Category,
                        options = question.Options.Select(option => new
                        {
                            answerId = option.AnswerId,
                            text = option.Text
                        }),
                        expiresAtUtc = expiresAt,
                        serverTimeUtc = DateTimeOffset.UtcNow
                    });

                    var completion = new TaskCompletionSource(
                         TaskCreationOptions.RunContinuationsAsynchronously);

                _questionCompletions[matchId] = completion;

                var timeoutTask = Task.Delay(
                    TimeSpan.FromSeconds(
                        TriviaScoringService.QuestionTimeSeconds));

                await Task.WhenAny(
                    completion.Task,
                    timeoutTask);

                _questionCompletions.TryRemove(
                    matchId,
                    out _);

            var closedAt = DateTimeOffset.UtcNow;

            var answers = match.CurrentAnswers.ToList();

            var finished = _gameService.CloseCurrentQuestion(
                match,
                closedAt);

            var nextQuestionAt = finished
                ? (DateTimeOffset?)null
                : closedAt.AddSeconds(5);

            foreach (var player in match.Players)
            {
                var answer = answers.FirstOrDefault(
                    answer => answer.UserId == player.UserId &&
                              answer.QuestionId == question.QuestionId);

                await _hubContext.Clients
                    .Group($"{matchId}:{player.UserId}")
                    .SendAsync(
                        "QuestionClosed",
                        new
                        {
                            questionId = question.QuestionId,
                            correctAnswerId = question.CorrectAnswerId,
                            selectedAnswerId = answer?.AnswerId,
                            isCorrect = answer?.IsCorrect ?? false,
                            pointsEarned = answer?.PointsAwarded ?? 0,
                            totalScore = player.Score,
                            nextQuestionAtUtc = nextQuestionAt,
                            serverTimeUtc = DateTimeOffset.UtcNow
                        });
            }

            var scoreboard = match.Players
                .OrderByDescending(player => player.Score)
                .ThenBy(player => player.CorrectAnswersTimeMs)
                .Select(player => new
                {
                    userId = player.UserId,
                    displayName = player.DisplayName,
                    score = player.Score,
                    isConnected = true
                })
                .ToList();

            await _hubContext.Clients
                .Group(matchId)
                .SendAsync(
                    "ScoreboardUpdated",
                    new
                    {
                        players = scoreboard
                    });

            if (finished)
            {
                var winner = _gameService.GetWinner(match);

                var orderedPlayers = match.Players
                    .OrderByDescending(player => player.Score)
                    .ThenBy(player => player.CorrectAnswersTimeMs)
                    .ToList();

                var finalPlayers = orderedPlayers
                    .Select((player, index) => new
                    {
                        userId = player.UserId,
                        displayName = player.DisplayName,
                        score = player.Score,
                        correctAnswers = player.CorrectAnswers,
                        correctAnswersTimeMs = player.CorrectAnswersTimeMs,
                        position = index + 1
                    })
                    .ToList();

                var highestScore = match.Players.Count == 0
                    ? 0
                    : match.Players.Max(player => player.Score);

                var scoreTie = match.Players.Count(player =>
                    player.Score == highestScore) > 1;

                await _hubContext.Clients
                    .Group(matchId)
                    .SendAsync(
                        "GameFinished",
                        new
                        {
                            matchId = match.MatchId,
                            players = finalPlayers,
                            winnerUserId = winner?.UserId,
                            isTie = winner is null,
                            decidedByTiebreak =
                                scoreTie && winner is not null,
                            finishedAtUtc = match.FinishedAt
                        });

                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
    finally
{
    _questionCompletions.TryRemove(
        matchId,
        out _);

    _runningMatches.TryRemove(
        matchId,
        out _);
}
}
}