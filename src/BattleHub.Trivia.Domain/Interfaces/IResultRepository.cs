using BattleHub.Trivia.Domain.Entities;

namespace BattleHub.Trivia.Domain.Interfaces;

/// <summary>
/// Persistencia de resultados de Trivia Battle.
/// Cubre POST/GET de resultados, historial y estadísticas por jugador.
/// </summary>
public interface IResultRepository
{
    Task SaveAsync(MatchResult result, CancellationToken cancellationToken = default);

    /// <summary>Devuelve null si la partida no existe.</summary>
    Task<MatchResult?> GetByMatchIdAsync(string matchId, CancellationToken cancellationToken = default);

    /// <summary>Partidas en las que participó el jugador, de la más reciente a la más antigua. Lista vacía si no hay historial.</summary>
    Task<IReadOnlyList<MatchResult>> GetHistoryByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Estadísticas agregadas. Si el jugador no tiene partidas, los contadores quedan en cero.</summary>
    Task<PlayerStats> GetStatsByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
