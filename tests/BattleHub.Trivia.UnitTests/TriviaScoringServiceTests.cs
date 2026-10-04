using BattleHub.Trivia.Domain.Services;

namespace BattleHub.Trivia.UnitTests;

public sealed class TriviaScoringServiceTests
{
    private readonly TriviaScoringService _service = new();

    [Fact]
    public void CalculatePoints_CorrectAnswer_ReturnsPointsWithSpeedBonus()
    {
        var elapsed = TimeSpan.FromSeconds(5);

        var points = _service.CalculatePoints(
            isCorrect: true,
            elapsed);

        Assert.Equal(200, points);
    }

    [Fact]
    public void CalculatePoints_IncorrectAnswer_ReturnsZero()
    {
        var elapsed = TimeSpan.FromSeconds(5);

        var points = _service.CalculatePoints(
            isCorrect: false,
            elapsed);

        Assert.Equal(0, points);
    }

    [Fact]
    public void CalculatePoints_AtTimeLimit_ReturnsZero()
    {
        var elapsed = TimeSpan.FromSeconds(15);

        var points = _service.CalculatePoints(
            isCorrect: true,
            elapsed);

        Assert.Equal(0, points);
    }
}