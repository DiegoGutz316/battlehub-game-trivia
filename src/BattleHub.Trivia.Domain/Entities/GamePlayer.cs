namespace BattleHub.Trivia.Domain.Entities;

public sealed class GamePlayer
{
    public required string UserId { get; init; }

    public required string DisplayName { get; init; }

    public int Score { get; set; }

    public int CorrectAnswers { get; set; }

    public long CorrectAnswersTimeMs { get; set; }
}