using BattleHub.Trivia.Api.Dtos;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using BattleHub.Trivia.Domain.Services;
using BattleHub.Trivia.Api.Services;

namespace BattleHub.Trivia.Api.Hubs;

public sealed class TriviaHub : Hub
{
    private readonly IGameRepository _gameRepository;
    private readonly IWebHostEnvironment _environment;
    private readonly TriviaGameService _gameService;
    private readonly TriviaMatchRunner _matchRunner;

    public TriviaHub(
    IGameRepository gameRepository,
    IWebHostEnvironment environment,
    TriviaGameService gameService,
    TriviaMatchRunner matchRunner)
{
    _gameRepository = gameRepository;
    _environment = environment;
    _gameService = gameService;
    _matchRunner = matchRunner;     
}

    public async Task JoinMatch(JoinMatchRequest request)
    {
        var match = _gameRepository.Get(request.MatchId);

        if (match is null)
        {
            await SendError(
                "MATCH_NOT_FOUND",
                "La partida no existe.");
            return;
        }

        if (match.IsFinished)
        {
            await SendError(
                "MATCH_FINISHED",
                "La partida ya terminó.");
            return;
        }

        if (!_environment.IsDevelopment())
        {
            await SendError(
                "UNAUTHORIZED",
                "La identidad simulada solo está disponible en desarrollo.");
            return;
        }

        var httpContext = Context.GetHttpContext();

        var userId =
            httpContext?.Request.Query["devUserId"].ToString();

        var displayName =
            httpContext?.Request.Query["devDisplayName"].ToString();

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(displayName))
        {
            await SendError(
                "UNAUTHORIZED",
                "No se pudo identificar al jugador.");
            return;
        }

        var player = match.Players
            .FirstOrDefault(player => player.UserId == userId);

        if (player is null)
        {
            await SendError(
                "PLAYER_NOT_IN_MATCH",
                "El jugador no pertenece a la partida.");
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            request.MatchId);
        
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"{request.MatchId}:{userId}");

        var players = match.Players
            .Select(player => new PlayerDto(
                player.UserId,
                player.DisplayName,
                player.Score,
                player.UserId == userId))
            .ToList();

        var state = new CurrentStateEvent(
            match.MatchId,
            "waiting",
            DateTimeOffset.UtcNow,
            match.Questions.Count,
            players,
            null,
            null);

        await Clients.Caller.SendAsync(
            "CurrentState",
            state);

        await Clients.Group(request.MatchId).SendAsync(
            "PlayerJoined",
            new
            {
                userId = player.UserId,
                displayName = player.DisplayName
            });
    }

    public async Task SubmitAnswer(SubmitAnswerRequest request)
{
    var match = _gameRepository.Get(request.MatchId);

    if (match is null)
    {
        await SendError(
            "MATCH_NOT_FOUND",
            "La partida no existe.");
        return;
    }

    var userId = Context.GetHttpContext()?
        .Request.Query["devUserId"]
        .ToString();

    if (string.IsNullOrWhiteSpace(userId))
    {
        await SendError(
            "PLAYER_NOT_IN_MATCH",
            "No se pudo identificar al jugador.");
        return;
    }

    var player = match.Players
        .FirstOrDefault(player => player.UserId == userId);

    if (player is null)
    {
        await SendError(
            "PLAYER_NOT_IN_MATCH",
            "El jugador no pertenece a la partida.");
        return;
    }

    var question = match.CurrentQuestion;

    if (question is null ||
        match.QuestionStartedAt is null ||
        question.QuestionId != request.QuestionId)
    {
        await SendError(
            "QUESTION_NOT_ACTIVE",
            "La pregunta indicada no es la pregunta activa.");
        return;
    }

    if (match.CurrentAnswers.Any(answer =>
        answer.UserId == userId &&
        answer.QuestionId == request.QuestionId))
    {
        await SendError(
            "ALREADY_ANSWERED",
            "El jugador ya respondió esta pregunta.");
        return;
    }

    if (!question.Options.Any(option =>
        option.AnswerId == request.AnswerId))
    {
        await SendError(
            "INVALID_ANSWER",
            "La opción seleccionada no pertenece a la pregunta.");
        return;
    }

    var receivedAt = DateTimeOffset.UtcNow;

    var expiresAt = match.QuestionStartedAt.Value.AddSeconds(
        TriviaScoringService.QuestionTimeSeconds);

    if (receivedAt >= expiresAt)
    {
        await SendError(
            "QUESTION_CLOSED",
            "El tiempo para responder esta pregunta terminó.");
        return;
    }

    _gameService.SubmitAnswer(
        match,
        userId,
        request.AnswerId,
        receivedAt);

    _matchRunner.NotifyAnswerReceived(request.MatchId);

    await Clients.Caller.SendAsync(
        "AnswerReceived",
        new
        {
            questionId = request.QuestionId,
            answerId = request.AnswerId,
            receivedAtUtc = receivedAt
        });
}

    private Task SendError(
        string code,
        string message)
    {
        return Clients.Caller.SendAsync(
            "GameError",
            new GameErrorEvent(code, message));
    }
}