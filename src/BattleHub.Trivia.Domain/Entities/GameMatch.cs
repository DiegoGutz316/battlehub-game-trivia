namespace BattleHub.Trivia.Domain.Entities;

public sealed class GameMatch
{
    public required string MatchId { get; init; }

    public List<GamePlayer> Players { get; } = [];

    public IReadOnlyList<Question> Questions { get; set; } = [];

    public int CurrentQuestionIndex { get; set; } = -1;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? QuestionStartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public List<PlayerAnswer> CurrentAnswers { get; } = [];

    public bool IsFinished { get; set; }

    public Question? CurrentQuestion =>
        CurrentQuestionIndex >= 0 &&
        CurrentQuestionIndex < Questions.Count
            ? Questions[CurrentQuestionIndex]
            : null;
}