using BattleHub.Trivia.Domain.Entities;

namespace BattleHub.Trivia.Domain.Services;

public sealed class TriviaGameService
{
    private readonly TriviaScoringService _scoringService;

    public TriviaGameService(TriviaScoringService scoringService)
    {
        _scoringService = scoringService;
    }

    public GameMatch CreateMatch(
        string matchId,
        IReadOnlyList<Question> questions)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            throw new ArgumentException(
                "El identificador de la partida es obligatorio.",
                nameof(matchId));
        }

        if (questions.Count == 0)
        {
            throw new ArgumentException(
                "La partida debe tener preguntas.",
                nameof(questions));
        }

        return new GameMatch
        {
            MatchId = matchId,
            Questions = questions,
            StartedAt = DateTimeOffset.UtcNow
        };
    }

    public void AddPlayer(
        GameMatch match,
        string userId,
        string displayName)
    {
        if (match.Players.Any(player => player.UserId == userId))
        {
            return;
        }

        match.Players.Add(new GamePlayer
        {
            UserId = userId,
            DisplayName = displayName
        });
    }

    public int SubmitAnswer(
    GameMatch match,
    string userId,
    string answerId,
    DateTimeOffset answeredAt)
{
    var player = match.Players
        .FirstOrDefault(player => player.UserId == userId);

    if (player is null)
    {
        throw new InvalidOperationException(
            "El jugador no pertenece a la partida.");
    }

    var question = match.CurrentQuestion;

    if (question is null || match.QuestionStartedAt is null)
    {
        throw new InvalidOperationException(
            "No hay una pregunta activa.");
    }

    if (match.CurrentAnswers.Any(answer =>
        answer.UserId == userId &&
        answer.QuestionId == question.QuestionId))
    {
        throw new InvalidOperationException(
            "El jugador ya respondió esta pregunta.");
    }

    if (!question.Options.Any(option =>
        option.AnswerId == answerId))
    {
        throw new ArgumentException(
            "La respuesta seleccionada no es válida.",
            nameof(answerId));
    }

    var elapsed = answeredAt - match.QuestionStartedAt.Value;

    if (elapsed < TimeSpan.Zero ||
        elapsed >= TimeSpan.FromSeconds(
            TriviaScoringService.QuestionTimeSeconds))
    {
        throw new InvalidOperationException(
            "El tiempo para responder terminó.");
    }

    var isCorrect = question.CorrectAnswerId == answerId;

    var points = _scoringService.CalculatePoints(
        isCorrect,
        elapsed);

    match.CurrentAnswers.Add(new PlayerAnswer
    {
        UserId = userId,
        QuestionId = question.QuestionId,
        AnswerId = answerId,
        AnsweredAt = answeredAt,
        IsCorrect = isCorrect,
        PointsAwarded = points
    });

    player.Score += points;

    if (isCorrect)
    {
        player.CorrectAnswers++;
        player.CorrectAnswersTimeMs +=
            (long)elapsed.TotalMilliseconds;
    }

    return points;
}
}