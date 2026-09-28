namespace BattleHub.Trivia.Domain.Entities;

public sealed record QuestionFilter(string? Category = null, string? Difficulty = null);
