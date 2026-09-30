using System.Text.Json;

namespace BattleHub.Trivia.Api.Dtos;

public sealed class SaveMatchResultRequest
{
    public string? MatchId { get; init; }

    public string? GameType { get; init; }

    public List<PlayerResultRequest>? Players { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public string? WinnerUserId { get; init; }

    public bool IsTie { get; init; }

    public bool DecidedByTiebreak { get; init; }

    public JsonElement Metadata { get; init; }
}

public sealed class PlayerResultRequest
{
    public string? UserId { get; init; }

    public string? DisplayName { get; init; }

    public int Score { get; init; }

    public int CorrectAnswers { get; init; }

    public long CorrectAnswersTimeMs { get; init; }

    public int Position { get; init; }
}

public sealed class MatchResultResponse
{
    public required string MatchId { get; init; }

    public required string GameType { get; init; }

    public required IReadOnlyList<PlayerResultResponse> Players { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }

    public string? WinnerUserId { get; init; }

    public bool IsTie { get; init; }

    public bool DecidedByTiebreak { get; init; }

    public required JsonElement Metadata { get; init; }
}

public sealed class PlayerResultResponse
{
    public required string UserId { get; init; }

    public required string DisplayName { get; init; }

    public int Score { get; init; }

    public int CorrectAnswers { get; init; }

    public long CorrectAnswersTimeMs { get; init; }

    public int Position { get; init; }
}

public sealed class PlayerStatsResponse
{
    public required string UserId { get; init; }

    public int MatchesPlayed { get; init; }

    public int Wins { get; init; }

    public double AverageScore { get; init; }

    public int BestScore { get; init; }
}
