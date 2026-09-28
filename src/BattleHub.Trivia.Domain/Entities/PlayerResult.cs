namespace BattleHub.Trivia.Domain.Entities;

public sealed class PlayerResult
{
    public required string UserId { get; init; }

    public required string DisplayName { get; init; }

    public int Score { get; init; }

    public int CorrectAnswers { get; init; }

    public long CorrectAnswersTimeMs { get; init; }

    public int Position { get; init; }
}
