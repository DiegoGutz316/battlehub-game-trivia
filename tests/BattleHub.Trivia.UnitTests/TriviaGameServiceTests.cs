using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Services;

namespace BattleHub.Trivia.UnitTests;

public sealed class TriviaGameServiceTests
{
    private readonly TriviaGameService _service =
        new(new TriviaScoringService());

    [Fact]
    public void SubmitAnswer_CorrectAnswer_AddsPoints()
    {
        var match = CreateStartedMatch();

        var points = _service.SubmitAnswer(
            match,
            "user-1",
            "a",
            match.QuestionStartedAt!.Value.AddSeconds(5));

        Assert.Equal(200, points);
        Assert.Equal(200, match.Players[0].Score);
        Assert.Equal(1, match.Players[0].CorrectAnswers);
    }

    [Fact]
    public void SubmitAnswer_IncorrectAnswer_AddsZeroPoints()
    {
        var match = CreateStartedMatch();

        var points = _service.SubmitAnswer(
            match,
            "user-1",
            "b",
            match.QuestionStartedAt!.Value.AddSeconds(5));

        Assert.Equal(0, points);
        Assert.Equal(0, match.Players[0].Score);
        Assert.Equal(0, match.Players[0].CorrectAnswers);
    }

    [Fact]
    public void SubmitAnswer_AfterTimeLimit_ThrowsException()
    {
        var match = CreateStartedMatch();

        Assert.Throws<InvalidOperationException>(() =>
            _service.SubmitAnswer(
                match,
                "user-1",
                "a",
                match.QuestionStartedAt!.Value.AddSeconds(16)));
    }

    [Fact]
public void GetWinner_PlayerWithHighestScore_ReturnsWinner()
{
    var match = CreateStartedMatch();

    _service.AddPlayer(
        match,
        "user-2",
        "Luis");

    match.Players[0].Score = 500;
    match.Players[1].Score = 300;

    _service.CloseCurrentQuestion(
        match,
        DateTimeOffset.UtcNow);

    var winner = _service.GetWinner(match);

    Assert.NotNull(winner);
    Assert.Equal("user-1", winner.UserId);
}

[Fact]
public void GetWinner_SameScoreAndSameTime_ReturnsNullForTie()
{
    var match = CreateStartedMatch();

    _service.AddPlayer(
        match,
        "user-2",
        "Luis");

    match.Players[0].Score = 500;
    match.Players[0].CorrectAnswersTimeMs = 5000;

    match.Players[1].Score = 500;
    match.Players[1].CorrectAnswersTimeMs = 5000;

    _service.CloseCurrentQuestion(
        match,
        DateTimeOffset.UtcNow);

    var winner = _service.GetWinner(match);

    Assert.Null(winner);
}

[Fact]
public void GetWinner_SameScoreButFasterTime_ReturnsFasterPlayer()
{
    var match = CreateStartedMatch();

    _service.AddPlayer(
        match,
        "user-2",
        "Luis");

    match.Players[0].Score = 500;
    match.Players[0].CorrectAnswersTimeMs = 4000;

    match.Players[1].Score = 500;
    match.Players[1].CorrectAnswersTimeMs = 6000;

    _service.CloseCurrentQuestion(
        match,
        DateTimeOffset.UtcNow);

    var winner = _service.GetWinner(match);

    Assert.NotNull(winner);
    Assert.Equal("user-1", winner.UserId);
}

    private GameMatch CreateStartedMatch()
    {
        var question = new Question
        {
            QuestionId = "q-001",
            Text = "¿Cuál es la capital de Costa Rica?",
            Category = "Geografía",
            Difficulty = "facil",
            CorrectAnswerId = "a",
            Options =
            [
                new AnswerOption
                {
                    AnswerId = "a",
                    Text = "San José"
                },
                new AnswerOption
                {
                    AnswerId = "b",
                    Text = "Cartago"
                }
            ]
        };

        var match = _service.CreateMatch(
            "match-001",
            [question]);

        _service.AddPlayer(
            match,
            "user-1",
            "Ana");

        _service.StartNextQuestion(
            match,
            DateTimeOffset.UtcNow);

        return match;
    }
}