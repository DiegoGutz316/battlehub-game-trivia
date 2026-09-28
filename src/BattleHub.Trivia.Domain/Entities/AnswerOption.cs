namespace BattleHub.Trivia.Domain.Entities;

public sealed class AnswerOption
{
    public required string AnswerId { get; init; }

    public required string Text { get; init; }
}
