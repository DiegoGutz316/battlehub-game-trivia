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

    [Fact]
    public void SubmitAnswer_JugadorAjeno_LanzaInvalidOperationException()
    {
        var match = CreateStartedMatch();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.SubmitAnswer(
                match,
                "user-99",
                "a",
                match.QuestionStartedAt!.Value.AddSeconds(5)));

        Assert.Equal("El jugador no pertenece a la partida.", exception.Message);
        Assert.Empty(match.CurrentAnswers);
    }

    [Fact]
    public void SubmitAnswer_SinIniciarPregunta_LanzaInvalidOperationException()
    {
        var match = _service.CreateMatch("match-001", [CreateQuestion("q-001")]);
        _service.AddPlayer(match, "user-1", "Ana");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.SubmitAnswer(match, "user-1", "a", DateTimeOffset.UtcNow));

        Assert.Equal("No hay una pregunta activa.", exception.Message);
        Assert.Empty(match.CurrentAnswers);
    }

    [Fact]
    public void SubmitAnswer_PreguntaCerrada_LanzaInvalidOperationException()
    {
        var match = CreateStartedMatchWithTwoQuestions();
        var startedAt = match.QuestionStartedAt!.Value;
        _service.CloseCurrentQuestion(match, startedAt.AddSeconds(15));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.SubmitAnswer(match, "user-1", "a", startedAt.AddSeconds(5)));

        Assert.Equal("No hay una pregunta activa.", exception.Message);
        Assert.Equal(0, match.Players[0].Score);
    }

    [Fact]
    public void SubmitAnswer_RespuestaRepetida_LanzaInvalidOperationExceptionYNoSumaPuntos()
    {
        var match = CreateStartedMatch();
        var startedAt = match.QuestionStartedAt!.Value;
        _service.SubmitAnswer(match, "user-1", "a", startedAt.AddSeconds(5));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.SubmitAnswer(match, "user-1", "a", startedAt.AddSeconds(6)));

        Assert.Equal("El jugador ya respondió esta pregunta.", exception.Message);
        Assert.Single(match.CurrentAnswers);
        Assert.Equal(200, match.Players[0].Score);
        Assert.Equal(1, match.Players[0].CorrectAnswers);
    }

    [Fact]
    public void SubmitAnswer_OpcionInexistente_LanzaArgumentException()
    {
        var match = CreateStartedMatch();

        var exception = Assert.Throws<ArgumentException>(() =>
            _service.SubmitAnswer(
                match,
                "user-1",
                "z",
                match.QuestionStartedAt!.Value.AddSeconds(5)));

        Assert.Equal("answerId", exception.ParamName);
        Assert.StartsWith("La respuesta seleccionada no es válida.", exception.Message);
        Assert.Empty(match.CurrentAnswers);
    }

    [Fact]
    public void SubmitAnswer_AntesDelInicioDeLaPregunta_LanzaInvalidOperationException()
    {
        var match = CreateStartedMatch();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.SubmitAnswer(
                match,
                "user-1",
                "a",
                match.QuestionStartedAt!.Value.AddSeconds(-1)));

        Assert.Equal("El tiempo para responder terminó.", exception.Message);
        Assert.Empty(match.CurrentAnswers);
        Assert.Equal(0, match.Players[0].Score);
    }

    [Fact]
    public void SubmitAnswer_JustoALos15Segundos_LanzaInvalidOperationException()
    {
        var match = CreateStartedMatch();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.SubmitAnswer(
                match,
                "user-1",
                "a",
                match.QuestionStartedAt!.Value.AddSeconds(
                    TriviaScoringService.QuestionTimeSeconds)));

        Assert.Equal("El tiempo para responder terminó.", exception.Message);
        Assert.Empty(match.CurrentAnswers);
    }

    [Theory]
    [InlineData(0, 240)]
    [InlineData(2500, 220)]
    [InlineData(14999, 100)]
    public void SubmitAnswer_RespuestaCorrectaATiempo_DevuelvePuntosSegunElTiempo(
        int elapsedMs,
        int expectedPoints)
    {
        var match = CreateStartedMatch();

        var points = _service.SubmitAnswer(
            match,
            "user-1",
            "a",
            match.QuestionStartedAt!.Value.AddMilliseconds(elapsedMs));

        Assert.Equal(expectedPoints, points);
        Assert.Equal(expectedPoints, match.Players[0].Score);
        Assert.Equal(elapsedMs, match.Players[0].CorrectAnswersTimeMs);
    }

    [Fact]
    public void SubmitAnswer_RespuestaIncorrecta_NoSumaTiempoParaDesempate()
    {
        var match = CreateStartedMatch();

        _service.SubmitAnswer(
            match,
            "user-1",
            "b",
            match.QuestionStartedAt!.Value.AddSeconds(3));

        var answer = Assert.Single(match.CurrentAnswers);
        Assert.False(answer.IsCorrect);
        Assert.Equal(0, answer.PointsAwarded);
        Assert.Equal(0, match.Players[0].CorrectAnswersTimeMs);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateMatch_SinMatchId_LanzaArgumentException(string matchId)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            _service.CreateMatch(matchId, [CreateQuestion("q-001")]));

        Assert.Equal("matchId", exception.ParamName);
        Assert.StartsWith("El identificador de la partida es obligatorio.", exception.Message);
    }

    [Fact]
    public void CreateMatch_SinPreguntas_LanzaArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            _service.CreateMatch("match-001", []));

        Assert.Equal("questions", exception.ParamName);
        Assert.StartsWith("La partida debe tener preguntas.", exception.Message);
    }

    [Fact]
    public void StartNextQuestion_PartidaTerminada_LanzaInvalidOperationException()
    {
        var match = CreateStartedMatch();
        _service.CloseCurrentQuestion(match, DateTimeOffset.UtcNow);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.StartNextQuestion(match, DateTimeOffset.UtcNow));

        Assert.Equal("La partida ya terminó.", exception.Message);
        Assert.Equal(0, match.CurrentQuestionIndex);
        Assert.Null(match.QuestionStartedAt);
    }

    [Fact]
    public void StartNextQuestion_SinPreguntasRestantes_LanzaInvalidOperationException()
    {
        var match = CreateStartedMatch();
        var startedAt = match.QuestionStartedAt;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.StartNextQuestion(match, DateTimeOffset.UtcNow.AddMinutes(1)));

        Assert.Equal("No quedan preguntas disponibles.", exception.Message);
        Assert.Equal(0, match.CurrentQuestionIndex);
        Assert.Equal(startedAt, match.QuestionStartedAt);
    }

    [Fact]
    public void CloseCurrentQuestion_PartidaTerminada_LanzaInvalidOperationException()
    {
        var match = CreateStartedMatch();
        var closedAt = DateTimeOffset.UtcNow;
        _service.CloseCurrentQuestion(match, closedAt);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.CloseCurrentQuestion(match, closedAt.AddSeconds(1)));

        Assert.Equal("La partida ya terminó.", exception.Message);
        Assert.Equal(closedAt, match.FinishedAt);
    }

    [Fact]
    public void CloseCurrentQuestion_SinIniciarPregunta_LanzaInvalidOperationException()
    {
        var match = _service.CreateMatch("match-001", [CreateQuestion("q-001")]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.CloseCurrentQuestion(match, DateTimeOffset.UtcNow));

        Assert.Equal("No hay una pregunta activa.", exception.Message);
        Assert.False(match.IsFinished);
    }

    [Fact]
    public void CloseCurrentQuestion_PreguntaYaCerrada_LanzaInvalidOperationException()
    {
        var match = CreateStartedMatchWithTwoQuestions();
        _service.CloseCurrentQuestion(match, DateTimeOffset.UtcNow);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.CloseCurrentQuestion(match, DateTimeOffset.UtcNow));

        Assert.Equal("No hay una pregunta activa.", exception.Message);
        Assert.False(match.IsFinished);
    }

    [Fact]
    public void CloseCurrentQuestion_NoEsLaUltima_DevuelveFalseYLaPartidaSigue()
    {
        var match = CreateStartedMatchWithTwoQuestions();
        var firstClosedAt = match.QuestionStartedAt!.Value.AddSeconds(15);

        var firstIsLast = _service.CloseCurrentQuestion(match, firstClosedAt);

        Assert.False(firstIsLast);
        Assert.False(match.IsFinished);
        Assert.Null(match.FinishedAt);
        Assert.Null(match.QuestionStartedAt);

        var second = _service.StartNextQuestion(match, firstClosedAt.AddSeconds(3));
        var secondClosedAt = firstClosedAt.AddSeconds(18);

        var secondIsLast = _service.CloseCurrentQuestion(match, secondClosedAt);

        Assert.Equal("q-002", second.QuestionId);
        Assert.True(secondIsLast);
        Assert.True(match.IsFinished);
        Assert.Equal(secondClosedAt, match.FinishedAt);
    }

    [Fact]
    public void GetWinner_PartidaSinTerminar_LanzaInvalidOperationException()
    {
        var match = CreateStartedMatch();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.GetWinner(match));

        Assert.Equal("La partida todavía no ha terminado.", exception.Message);
    }

    [Fact]
    public void GetWinner_SinJugadores_DevuelveNull()
    {
        var match = _service.CreateMatch("match-001", [CreateQuestion("q-001")]);
        _service.StartNextQuestion(match, DateTimeOffset.UtcNow);
        _service.CloseCurrentQuestion(match, DateTimeOffset.UtcNow);

        var winner = _service.GetWinner(match);

        Assert.Null(winner);
    }

    [Fact]
    public void GetWinner_MismosPuntosPorRespuesta_GanaQuienRespondioMasRapido()
    {
        var match = CreateStartedMatch();
        _service.AddPlayer(match, "user-2", "Luis");
        var startedAt = match.QuestionStartedAt!.Value;

        var anaPoints = _service.SubmitAnswer(match, "user-1", "a", startedAt.AddMilliseconds(2800));
        var luisPoints = _service.SubmitAnswer(match, "user-2", "a", startedAt.AddMilliseconds(2200));
        _service.CloseCurrentQuestion(match, startedAt.AddSeconds(15));

        var winner = _service.GetWinner(match);

        Assert.Equal(220, anaPoints);
        Assert.Equal(anaPoints, luisPoints);
        Assert.NotNull(winner);
        Assert.Equal("user-2", winner.UserId);
    }

    [Fact]
    public void GetWinner_EmpateEntreTres_DesempataSoloEntreLosDePuntajeMasAlto()
    {
        var match = CreateStartedMatch();
        _service.AddPlayer(match, "user-2", "Luis");
        _service.AddPlayer(match, "user-3", "Sofía");

        match.Players[0].Score = 500;
        match.Players[0].CorrectAnswersTimeMs = 6000;
        match.Players[1].Score = 500;
        match.Players[1].CorrectAnswersTimeMs = 4000;
        match.Players[2].Score = 300;
        match.Players[2].CorrectAnswersTimeMs = 1000;

        _service.CloseCurrentQuestion(match, DateTimeOffset.UtcNow);

        var winner = _service.GetWinner(match);

        Assert.NotNull(winner);
        Assert.Equal("user-2", winner.UserId);
    }

    [Fact]
    public void GetWinner_DosEmpatadosEnPuntosYTiempo_DevuelveNullAunqueUnTerceroSeaMasLento()
    {
        var match = CreateStartedMatch();
        _service.AddPlayer(match, "user-2", "Luis");
        _service.AddPlayer(match, "user-3", "Sofía");

        match.Players[0].Score = 500;
        match.Players[0].CorrectAnswersTimeMs = 4000;
        match.Players[1].Score = 500;
        match.Players[1].CorrectAnswersTimeMs = 4000;
        match.Players[2].Score = 500;
        match.Players[2].CorrectAnswersTimeMs = 9000;

        _service.CloseCurrentQuestion(match, DateTimeOffset.UtcNow);

        var winner = _service.GetWinner(match);

        Assert.Null(winner);
    }

    [Fact]
    public void AddPlayer_JugadorRepetido_NoLoDuplica()
    {
        var match = CreateStartedMatch();

        _service.AddPlayer(match, "user-1", "Otro nombre");

        var player = Assert.Single(match.Players);
        Assert.Equal("user-1", player.UserId);
        Assert.Equal("Ana", player.DisplayName);
    }

    private GameMatch CreateStartedMatchWithTwoQuestions()
    {
        var match = _service.CreateMatch(
            "match-002",
            [CreateQuestion("q-001"), CreateQuestion("q-002")]);

        _service.AddPlayer(match, "user-1", "Ana");
        _service.StartNextQuestion(match, DateTimeOffset.UtcNow);

        return match;
    }

    private static Question CreateQuestion(string questionId) => new()
    {
        QuestionId = questionId,
        Text = $"Pregunta {questionId}",
        Category = "Geografía",
        Difficulty = "facil",
        CorrectAnswerId = "a",
        Options =
        [
            new AnswerOption { AnswerId = "a", Text = "Opción A" },
            new AnswerOption { AnswerId = "b", Text = "Opción B" }
        ]
    };

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
