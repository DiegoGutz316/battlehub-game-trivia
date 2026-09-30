namespace BattleHub.Trivia.Data.Persistence;

public sealed class PlayerResultRecord
{
    public string MatchId { get; set; } = "";

    public MatchResultRecord? Match { get; set; }

    public string UserId { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public int Score { get; set; }

    public int CorrectAnswers { get; set; }

    public long CorrectAnswersTimeMs { get; set; }

    public int Position { get; set; }
}
