using System.Collections.Concurrent;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Interfaces;

namespace BattleHub.Trivia.Domain.Repositories;

public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<string, GameMatch> _matches = new();

    public GameMatch? Get(string matchId)
    {
        return _matches.TryGetValue(matchId, out var match)
            ? match
            : null;
    }

    public void Add(GameMatch match)
    {
        if (!_matches.TryAdd(match.MatchId, match))
        {
            throw new InvalidOperationException(
                "La partida ya existe.");
        }
    }

    public void Remove(string matchId)
    {
        _matches.TryRemove(matchId, out _);
    }
}