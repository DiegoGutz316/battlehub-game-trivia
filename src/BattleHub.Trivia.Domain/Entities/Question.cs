namespace BattleHub.Trivia.Domain.Entities;

public sealed class Question
{
    public required string QuestionId { get; init; }

    public required string Text { get; init; }

    public string? Category { get; init; }

    public string? Difficulty { get; init; }

    public required IReadOnlyList<AnswerOption> Options { get; init; }

    /// <summary>Solo para el servidor. No se envía al cliente mientras la pregunta está activa.</summary>
    public required string CorrectAnswerId { get; init; }
}
