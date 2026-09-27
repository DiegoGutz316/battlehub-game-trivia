namespace BattleHub.Trivia.Domain.Entities;

public sealed class PlayerStats
{
    public required string UserId { get; init; }

    public int MatchesPlayed { get; init; }

    public int Wins { get; init; }

    public double AverageScore { get; init; }

    public int BestScore { get; init; }
}
