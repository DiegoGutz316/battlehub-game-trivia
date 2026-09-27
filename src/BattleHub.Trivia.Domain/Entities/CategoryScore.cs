namespace BattleHub.Trivia.Domain.Entities;

public sealed class CategoryScore
{
    public required string UserId { get; init; }

    public required string Category { get; init; }

    public int CorrectAnswers { get; init; }

    public int IncorrectAnswers { get; init; }
}
