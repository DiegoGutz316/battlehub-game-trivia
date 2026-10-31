using System.Text.Json;
using BattleHub.Trivia.Api.Controllers;
using BattleHub.Trivia.Api.Dtos;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Exceptions;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace BattleHub.Trivia.UnitTests;

public sealed class ResultsControllerTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 10, 1, 18, 0, 0, TimeSpan.Zero);

    private readonly IResultRepository _repository =
        Substitute.For<IResultRepository>();

    [Theory]
    [InlineData("matchId vacío", "matchId es obligatorio.")]
    [InlineData("matchId con espacios", "matchId es obligatorio.")]
    [InlineData("sin startedAt", "startedAt y finishedAt son obligatorios.")]
    [InlineData("sin finishedAt", "startedAt y finishedAt son obligatorios.")]
    [InlineData("fin antes del inicio", "finishedAt no puede ser anterior a startedAt.")]
    [InlineData("otro gameType", "gameType debe ser trivia.")]
    [InlineData("metadata arreglo", "metadata debe ser un objeto JSON.")]
    [InlineData("metadata texto", "metadata debe ser un objeto JSON.")]
    [InlineData("players null", "players debe incluir al menos un jugador.")]
    [InlineData("players vacío", "players debe incluir al menos un jugador.")]
    [InlineData("jugador sin userId", "Cada jugador necesita userId y displayName.")]
    [InlineData("jugador sin displayName", "Cada jugador necesita userId y displayName.")]
    [InlineData("userId repetido", "userId no puede repetirse dentro de la misma partida.")]
    [InlineData("ganador ajeno", "winnerUserId debe ser uno de los jugadores de la partida.")]
    public async Task Create_DatosInvalidos_DevuelveBadRequestYNoGuarda(
        string caso,
        string expectedMessage)
    {
        var controller = CreateController();

        var result = await controller.Create(InvalidRequest(caso), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(expectedMessage, ReadMessage(badRequest.Value));
        await _repository.DidNotReceive()
            .SaveAsync(Arg.Any<MatchResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_DatosValidos_GuardaLimpiandoEspaciosYDevuelveCreated()
    {
        MatchResult? saved = null;
        _repository
            .SaveAsync(Arg.Do<MatchResult>(match => saved = match), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var controller = CreateController();

        var result = await controller.Create(
            CreateRequest(
                matchId: "  match-001  ",
                winnerUserId: " user-1 ",
                players:
                [
                    CreatePlayer(" user-1 ", " Ana ", 500),
                    CreatePlayer("user-2", "Luis", 300)
                ]),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ResultsController.GetByMatchId), created.ActionName);
        Assert.Equal("match-001", created.RouteValues!["matchId"]);
        var response = Assert.IsType<MatchResultResponse>(created.Value);
        Assert.Equal("match-001", response.MatchId);
        Assert.Equal("trivia", response.GameType);

        await _repository.Received(1)
            .SaveAsync(Arg.Any<MatchResult>(), Arg.Any<CancellationToken>());
        Assert.NotNull(saved);
        Assert.Equal("match-001", saved.MatchId);
        Assert.Equal("user-1", saved.WinnerUserId);
        Assert.Equal("user-1", saved.Players[0].UserId);
        Assert.Equal("Ana", saved.Players[0].DisplayName);
    }

    [Fact]
    public async Task Create_EmpateSinGanador_GuardaSinGanadorYMarcaElEmpate()
    {
        MatchResult? saved = null;
        _repository
            .SaveAsync(Arg.Do<MatchResult>(match => saved = match), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var controller = CreateController();
        var request = new SaveMatchResultRequest
        {
            MatchId = "match-empate",
            GameType = "trivia",
            StartedAt = StartedAt,
            FinishedAt = StartedAt.AddMinutes(3),
            WinnerUserId = null,
            IsTie = true,
            DecidedByTiebreak = false,
            Players =
            [
                CreatePlayer("user-1", "Ana", 500),
                CreatePlayer("user-2", "Luis", 500)
            ]
        };

        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<MatchResultResponse>(created.Value);
        Assert.Null(response.WinnerUserId);
        Assert.True(response.IsTie);
        Assert.NotNull(saved);
        Assert.Null(saved.WinnerUserId);
        Assert.True(saved.IsTie);
        Assert.False(saved.DecidedByTiebreak);
        Assert.Equal(2, saved.Players.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  TRIVIA  ")]
    public async Task Create_GameTypeVacioOEnMayusculas_LoGuardaComoTrivia(string? gameType)
    {
        var controller = CreateController();

        var result = await controller.Create(
            CreateRequest(gameType: gameType),
            CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result.Result);
        await _repository.Received(1).SaveAsync(
            Arg.Is<MatchResult>(match => match.GameType == MatchResult.GameTypeName),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_PartidaRepetida_DevuelveConflictConElMensaje()
    {
        _repository
            .SaveAsync(Arg.Any<MatchResult>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MatchAlreadyExistsException("match-001"));
        var controller = CreateController();

        var result = await controller.Create(CreateRequest(), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(
            "Ya existe un resultado para la partida match-001.",
            ReadMessage(conflict.Value));
        await _repository.Received(1).SaveAsync(
            Arg.Is<MatchResult>(match => match.MatchId == "match-001"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("null")]
    public async Task Create_MetadataNullOAusente_GuardaMetadataVacia(string? metadataJson)
    {
        MatchResult? saved = null;
        _repository
            .SaveAsync(Arg.Do<MatchResult>(match => saved = match), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var controller = CreateController();

        var result = await controller.Create(
            CreateRequest(metadata: ParseJson(metadataJson)),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.NotNull(saved);
        Assert.Empty(saved.Metadata.CategoryScores);
        var response = Assert.IsType<MatchResultResponse>(created.Value);
        Assert.Equal(JsonValueKind.Object, response.Metadata.ValueKind);
        Assert.Empty(response.Metadata.EnumerateObject());
    }

    [Fact]
    public async Task GetByMatchId_PartidaInexistente_DevuelveNotFound()
    {
        _repository
            .GetByMatchIdAsync("match-404", Arg.Any<CancellationToken>())
            .Returns((MatchResult?)null);
        var controller = CreateController();

        var result = await controller.GetByMatchId("match-404", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
        await _repository.Received(1)
            .GetByMatchIdAsync("match-404", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByMatchId_PartidaExistente_DevuelveOk()
    {
        _repository
            .GetByMatchIdAsync("match-001", Arg.Any<CancellationToken>())
            .Returns(CreateStoredMatch());
        var controller = CreateController();

        var result = await controller.GetByMatchId("match-001", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<MatchResultResponse>(ok.Value);
        Assert.Equal("match-001", response.MatchId);
        Assert.Equal(new[] { "user-1", "user-2" }, response.Players.Select(player => player.UserId));
    }

    [Fact]
    public async Task GetStats_DevuelveLasEstadisticasDelJugador()
    {
        _repository
            .GetStatsByUserIdAsync("user-2", Arg.Any<CancellationToken>())
            .Returns(new PlayerStats
            {
                UserId = "user-2",
                MatchesPlayed = 4,
                Wins = 1,
                AverageScore = 350.5,
                BestScore = 720
            });
        var controller = CreateController();

        var result = await controller.GetStats("user-2", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var stats = Assert.IsType<PlayerStatsResponse>(ok.Value);
        Assert.Equal("user-2", stats.UserId);
        Assert.Equal(4, stats.MatchesPlayed);
        Assert.Equal(1, stats.Wins);
        Assert.Equal(350.5, stats.AverageScore);
        Assert.Equal(720, stats.BestScore);
    }

    private ResultsController CreateController() => new(_repository);

    private static SaveMatchResultRequest InvalidRequest(string caso) => caso switch
    {
        "matchId vacío" => CreateRequest(matchId: ""),
        "matchId con espacios" => CreateRequest(matchId: "   "),
        "sin startedAt" => CreateRequest(withStartedAt: false),
        "sin finishedAt" => CreateRequest(withFinishedAt: false),
        "fin antes del inicio" => CreateRequest(duration: TimeSpan.FromSeconds(-1)),
        "otro gameType" => CreateRequest(gameType: "ajedrez"),
        "metadata arreglo" => CreateRequest(metadata: ParseJson("[]")),
        "metadata texto" => CreateRequest(metadata: ParseJson("\"texto\"")),
        "players null" => CreateRequest(withPlayers: false),
        "players vacío" => CreateRequest(players: []),
        "jugador sin userId" => CreateRequest(players: [CreatePlayer(" ", "Ana")]),
        "jugador sin displayName" => CreateRequest(players: [CreatePlayer("user-1", "")]),
        "userId repetido" => CreateRequest(
            players: [CreatePlayer("user-1", "Ana"), CreatePlayer("user-1", "Ana otra vez")]),
        "ganador ajeno" => CreateRequest(winnerUserId: "user-99"),
        _ => throw new ArgumentOutOfRangeException(nameof(caso), caso, null)
    };

    // Sin argumentos devuelve una petición válida de dos jugadores; cada argumento cambia una sola cosa.
    private static SaveMatchResultRequest CreateRequest(
        string? matchId = "match-001",
        string? gameType = "trivia",
        bool withStartedAt = true,
        bool withFinishedAt = true,
        TimeSpan? duration = null,
        string? winnerUserId = "user-1",
        JsonElement metadata = default,
        bool withPlayers = true,
        List<PlayerResultRequest>? players = null) => new()
    {
        MatchId = matchId,
        GameType = gameType,
        StartedAt = withStartedAt ? StartedAt : null,
        FinishedAt = withFinishedAt ? StartedAt + (duration ?? TimeSpan.FromMinutes(3)) : null,
        WinnerUserId = winnerUserId,
        Metadata = metadata,
        Players = withPlayers
            ? players ?? [CreatePlayer("user-1", "Ana", 500), CreatePlayer("user-2", "Luis", 300)]
            : null
    };

    private static PlayerResultRequest CreatePlayer(
        string? userId,
        string? displayName,
        int score = 0) => new()
    {
        UserId = userId,
        DisplayName = displayName,
        Score = score
    };

    private static MatchResult CreateStoredMatch() => new()
    {
        MatchId = "match-001",
        StartedAt = StartedAt,
        FinishedAt = StartedAt.AddMinutes(3),
        WinnerUserId = "user-1",
        Players =
        [
            new PlayerResult { UserId = "user-1", DisplayName = "Ana", Score = 500, Position = 1 },
            new PlayerResult { UserId = "user-2", DisplayName = "Luis", Score = 300, Position = 2 }
        ]
    };

    private static JsonElement ParseJson(string? json) =>
        json is null ? default : JsonDocument.Parse(json).RootElement.Clone();

    private static string? ReadMessage(object? value) =>
        JsonSerializer.SerializeToElement(value).GetProperty("message").GetString();
}
