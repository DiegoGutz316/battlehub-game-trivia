using BattleHub.Trivia.Domain.Services;
namespace BattleHub.Trivia.UnitTests;
public sealed class ScoreLimitTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void InstantAnswerNeverExceedsContractMaximum(int milliseconds)
        => Assert.Equal(240, new TriviaScoringService().CalculatePoints(true, TimeSpan.FromMilliseconds(milliseconds)));
}
