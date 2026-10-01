namespace BattleHub.Trivia.Data.Persistence;

public sealed class QuestionRecord
{
    public string QuestionId { get; set; } = "";

    public string Text { get; set; } = "";

    public string? Category { get; set; }

    public string? Difficulty { get; set; }

    public string CorrectAnswerId { get; set; } = "";

    public List<AnswerOptionRecord> Options { get; set; } = [];
}
