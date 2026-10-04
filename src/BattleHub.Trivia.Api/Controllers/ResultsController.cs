using System.Text.Json;
using BattleHub.Trivia.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using BattleHub.Trivia.Api.Dtos;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Exceptions;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BattleHub.Trivia.Api.Controllers;

[Authorize(Policy = TriviaAuth.Read)]
[ApiController]
[Route("api/games/trivia")]
public sealed class ResultsController(IResultRepository results) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    [Authorize(Policy = TriviaAuth.Write)]
    [HttpPost("results")]
    public async Task<ActionResult<MatchResultResponse>> Create(
        SaveMatchResultRequest request,
        CancellationToken cancellationToken)
    {
        var error = Validate(request);
        if (error is not null)
            return BadRequest(new { message = error });

        var match = ToDomain(request);

        try
        {
            await results.SaveAsync(match, cancellationToken);
        }
        catch (MatchAlreadyExistsException exception)
        {
            return Conflict(new { message = exception.Message });
        }

        var response = ToResponse(match);
        return CreatedAtAction(nameof(GetByMatchId), new { matchId = match.MatchId }, response);
    }

    [HttpGet("results/{matchId}")]
    public async Task<ActionResult<MatchResultResponse>> GetByMatchId(
        string matchId,
        CancellationToken cancellationToken)
    {
        var match = await results.GetByMatchIdAsync(matchId, cancellationToken);
        if (match is null)
            return NotFound();

        if (!TriviaAuth.IsWriter(User) && !match.Players.Any(p => p.UserId == TriviaAuth.UserId(User))) return Forbid();
        return Ok(ToResponse(match));
    }

    [HttpGet("players/{userId}/history")]
    public async Task<ActionResult<IReadOnlyList<MatchResultResponse>>> GetHistory(
        string userId,
        CancellationToken cancellationToken)
    {
        if (!TriviaAuth.IsWriter(User) && userId != TriviaAuth.UserId(User)) return Forbid();
        var history = await results.GetHistoryByUserIdAsync(userId, cancellationToken);
        return Ok(history.Select(ToResponse).ToList());
    }

    [HttpGet("players/{userId}/stats")]
    public async Task<ActionResult<PlayerStatsResponse>> GetStats(
        string userId,
        CancellationToken cancellationToken)
    {
        if (!TriviaAuth.IsWriter(User) && userId != TriviaAuth.UserId(User)) return Forbid();
        var stats = await results.GetStatsByUserIdAsync(userId, cancellationToken);
        return Ok(new PlayerStatsResponse
        {
            UserId = stats.UserId,
            MatchesPlayed = stats.MatchesPlayed,
            Wins = stats.Wins,
            AverageScore = stats.AverageScore,
            BestScore = stats.BestScore
        });
    }

    private static string? Validate(SaveMatchResultRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.MatchId))
            return "matchId es obligatorio.";

        if (request.StartedAt is null || request.FinishedAt is null)
            return "startedAt y finishedAt son obligatorios.";

        if (request.FinishedAt < request.StartedAt)
            return "finishedAt no puede ser anterior a startedAt.";

        var gameType = string.IsNullOrWhiteSpace(request.GameType)
            ? MatchResult.GameTypeName
            : request.GameType.Trim();

        if (!gameType.Equals(MatchResult.GameTypeName, StringComparison.OrdinalIgnoreCase))
            return "gameType debe ser trivia.";

        var metadataKind = request.Metadata.ValueKind;
        if (metadataKind is not JsonValueKind.Undefined &&
            metadataKind is not JsonValueKind.Null &&
            metadataKind is not JsonValueKind.Object)
            return "metadata debe ser un objeto JSON.";

        if (request.Players is null || request.Players.Count == 0)
            return "players debe incluir al menos un jugador.";

        if (request.Players.Any(player => string.IsNullOrWhiteSpace(player.UserId) || string.IsNullOrWhiteSpace(player.DisplayName)))
            return "Cada jugador necesita userId y displayName.";

        if (request.Players.Select(player => player.UserId).Distinct(StringComparer.Ordinal).Count() != request.Players.Count)
            return "userId no puede repetirse dentro de la misma partida.";

        if (request.WinnerUserId is not null &&
            request.Players.All(player => player.UserId != request.WinnerUserId))
            return "winnerUserId debe ser uno de los jugadores de la partida.";

        return null;
    }

    private static MatchResult ToDomain(SaveMatchResultRequest request) => new()
    {
        MatchId = request.MatchId!.Trim(),
        GameType = MatchResult.GameTypeName,
        StartedAt = request.StartedAt!.Value,
        FinishedAt = request.FinishedAt!.Value,
        WinnerUserId = string.IsNullOrWhiteSpace(request.WinnerUserId) ? null : request.WinnerUserId.Trim(),
        IsTie = request.IsTie,
        DecidedByTiebreak = request.DecidedByTiebreak,
        Metadata = ReadMetadata(request.Metadata),
        Players = request.Players!.Select(player => new PlayerResult
        {
            UserId = player.UserId!.Trim(),
            DisplayName = player.DisplayName!.Trim(),
            Score = player.Score,
            CorrectAnswers = player.CorrectAnswers,
            CorrectAnswersTimeMs = player.CorrectAnswersTimeMs,
            Position = player.Position
        }).ToList()
    };

    private static MatchResultResponse ToResponse(MatchResult match) => new()
    {
        MatchId = match.MatchId,
        GameType = match.GameType,
        StartedAt = match.StartedAt,
        FinishedAt = match.FinishedAt,
        WinnerUserId = match.WinnerUserId,
        IsTie = match.IsTie,
        DecidedByTiebreak = match.DecidedByTiebreak,
        Metadata = WriteMetadata(match.Metadata),
        Players = match.Players.Select(player => new PlayerResultResponse
        {
            UserId = player.UserId,
            DisplayName = player.DisplayName,
            Score = player.Score,
            CorrectAnswers = player.CorrectAnswers,
            CorrectAnswersTimeMs = player.CorrectAnswersTimeMs,
            Position = player.Position
        }).ToList()
    };

    private static TriviaMetadata ReadMetadata(JsonElement metadata)
    {
        if (metadata.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return new TriviaMetadata();

        return JsonSerializer.Deserialize<TriviaMetadata>(metadata, JsonOptions) ?? new TriviaMetadata();
    }

    private static JsonElement WriteMetadata(TriviaMetadata metadata)
    {
        if (metadata.CategoryScores.Count == 0)
            return JsonSerializer.Deserialize<JsonElement>("{}")!;

        return JsonSerializer.SerializeToElement(metadata, JsonOptions);
    }
}
