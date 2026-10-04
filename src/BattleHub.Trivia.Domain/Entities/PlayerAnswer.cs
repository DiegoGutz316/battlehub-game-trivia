namespace BattleHub.Trivia.Domain.Entities;

public sealed class PlayerAnswer
{
    public required string UserId { get; init; }

    public required string QuestionId { get; init; }

    public required string AnswerId { get; init; }

    public required DateTimeOffset AnsweredAt { get; init; }

    public bool IsCorrect { get; init; }

    public int PointsAwarded { get; init; }
}