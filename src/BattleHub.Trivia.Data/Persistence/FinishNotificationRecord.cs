namespace BattleHub.Trivia.Data.Persistence;

public sealed class FinishNotificationRecord
{
    public string MatchId { get; set; } = "";
    public DateTimeOffset NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public bool Delivered { get; set; }
    public bool Blocked { get; set; }
}
