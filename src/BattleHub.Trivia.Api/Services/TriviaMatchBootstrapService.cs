using BattleHub.Trivia.Domain.Interfaces;
using BattleHub.Trivia.Domain.Services;

namespace BattleHub.Trivia.Api.Services;

/// <summary>
/// Prepara partidas de Trivia temporalmente durante E2.
/// Será reemplazado por la integración real con Matchmaking.
/// </summary>
public sealed class TriviaMatchBootstrapService
{
    private readonly IGameRepository _gameRepository;
    private readonly IQuestionRepository _questionRepository;
    private readonly TriviaGameService _gameService;

    public TriviaMatchBootstrapService(
        IGameRepository gameRepository,
        IQuestionRepository questionRepository,
        TriviaGameService gameService)
    {
        _gameRepository = gameRepository;
        _questionRepository = questionRepository;
        _gameService = gameService;
    }

    public async Task PrepareMatchAsync(
        string matchId,
        IReadOnlyList<(string UserId, string DisplayName)> players,
        CancellationToken cancellationToken = default)
    {
        if (_gameRepository.Get(matchId) is not null)
        {
            return;
        }

        var questions = await _questionRepository.DrawAsync(
            10,
            cancellationToken: cancellationToken);

        if (questions.Count == 0)
        {
            throw new InvalidOperationException(
                "No hay preguntas disponibles para iniciar la partida.");
        }

        var match = _gameService.CreateMatch(
            matchId,
            questions);

        foreach (var player in players)
        {
            _gameService.AddPlayer(
                match,
                player.UserId,
                player.DisplayName);
        }

        _gameRepository.Add(match);
    }
}