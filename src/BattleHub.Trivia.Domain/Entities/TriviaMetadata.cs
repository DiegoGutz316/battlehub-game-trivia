namespace BattleHub.Trivia.Domain.Entities;

public sealed class TriviaMetadata
{
    public IReadOnlyList<CategoryScore> CategoryScores { get; init; } = [];
}
