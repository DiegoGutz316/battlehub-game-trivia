namespace BattleHub.Trivia.Domain.Exceptions;

public sealed class MatchAlreadyExistsException(string matchId)
    : Exception($"Ya existe un resultado para la partida {matchId}.")
{
    public string MatchId { get; } = matchId;
}
