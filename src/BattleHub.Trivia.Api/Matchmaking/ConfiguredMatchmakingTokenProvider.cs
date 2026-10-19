
namespace BattleHub.Trivia.Api.Matchmaking;

public sealed class ConfiguredMatchmakingTokenProvider(
    IConfiguration configuration) : IMatchmakingTokenProvider
{
    public Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var token = configuration["Matchmaking:ServiceToken"];

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "Configurar Matchmaking:ServiceToken para utilizar la integración con Matchmaking.");
        }

        return Task.FromResult(token);
    }
}
