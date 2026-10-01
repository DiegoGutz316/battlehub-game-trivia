namespace BattleHub.Trivia.Data.Persistence;

public sealed class AnswerOptionRecord
{
    public string QuestionId { get; set; } = "";

    public QuestionRecord? Question { get; set; }

    public string AnswerId { get; set; } = "";

    public string Text { get; set; } = "";
}
