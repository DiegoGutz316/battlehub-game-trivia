namespace BattleHub.Trivia.Api.Matchmaking;

public interface IMatchmakingTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken);
}
