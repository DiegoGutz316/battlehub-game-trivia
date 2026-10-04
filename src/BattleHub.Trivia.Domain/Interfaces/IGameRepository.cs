using BattleHub.Trivia.Domain.Entities;

namespace BattleHub.Trivia.Domain.Interfaces;

public interface IGameRepository
{
    GameMatch? Get(string matchId);

    void Add(GameMatch match);

    void Remove(string matchId);
}