namespace BattleHub.Trivia.Domain.Services;

public sealed class TriviaScoringService
{
    public const int QuestionTimeSeconds = 15;
    public const int BasePoints = 100;
    public const int PointsPerRemainingSecond = 10;

    public int CalculatePoints(bool isCorrect, TimeSpan elapsed)
    {
        if (!isCorrect || elapsed < TimeSpan.Zero)
        {
            return 0;
        }

        if (elapsed >= TimeSpan.FromSeconds(QuestionTimeSeconds))
        {
            return 0;
        }

        var remainingSeconds =
            (int)Math.Floor(QuestionTimeSeconds - elapsed.TotalSeconds);

        return Math.Min(240, BasePoints + (remainingSeconds * PointsPerRemainingSecond));
    }
}