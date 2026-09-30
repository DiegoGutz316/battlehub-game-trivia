namespace BattleHub.Trivia.Data.Persistence;

public sealed class MatchResultRecord
{
    public string MatchId { get; set; } = "";

    public string GameType { get; set; } = "trivia";

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FinishedAt { get; set; }

    public string? WinnerUserId { get; set; }

    public bool IsTie { get; set; }

    public bool DecidedByTiebreak { get; set; }

    public string MetadataJson { get; set; } = "{}";

    public List<PlayerResultRecord> Players { get; set; } = [];
}
