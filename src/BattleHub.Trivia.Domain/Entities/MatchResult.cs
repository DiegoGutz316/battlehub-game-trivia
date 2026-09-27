namespace BattleHub.Trivia.Domain.Entities;

public sealed class MatchResult
{
    public const string GameTypeName = "trivia";

    public required string MatchId { get; init; }

    public string GameType { get; init; } = GameTypeName;

    public required IReadOnlyList<PlayerResult> Players { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }

    public string? WinnerUserId { get; init; }

    public bool IsTie { get; init; }

    public bool DecidedByTiebreak { get; init; }

    public TriviaMetadata Metadata { get; init; } = new();
}
