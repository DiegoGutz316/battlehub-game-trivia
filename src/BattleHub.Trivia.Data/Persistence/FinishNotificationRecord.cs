namespace BattleHub.Trivia.Data.Persistence;

public sealed class FinishNotificationRecord
{
    public Guid Id { get; set; }
    public Guid MatchRefId { get; set; }
    public string MatchId { get; set; } = "";
    public DateTimeOffset NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public bool Delivered { get; set; }
    public bool Blocked { get; set; }
}
