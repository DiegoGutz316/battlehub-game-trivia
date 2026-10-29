using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BattleHub.Trivia.Api.Matchmaking;

// Token de servicio (client credentials) para avisar el fin de partida a Matchmaking.
// Lo obtiene el servidor, lo guarda en memoria y lo renueva antes de que venza; nunca se pide al jugador.
public sealed class ClientCredentialsTokenProvider(
    HttpClient http,
    IConfiguration config,
    TimeProvider clock) : IMatchmakingTokenProvider
{
    private sealed record CachedToken(string Value, DateTimeOffset RenewAt);
    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private readonly SemaphoreSlim gate = new(1, 1);
    private CachedToken? cached;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (cached is { } current && clock.GetUtcNow() < current.RenewAt)
        {
            return current.Value;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cached is { } renewed && clock.GetUtcNow() < renewed.RenewAt)
            {
                return renewed.Value;
            }

            var endpoint = config["Matchmaking:TokenEndpoint"];
            var clientId = config["Matchmaking:ClientId"];
            var clientSecret = config["Matchmaking:ClientSecret"];
            var audience = config["Matchmaking:Audience"];
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidOperationException(
                    "Configurar Matchmaking:TokenEndpoint, Matchmaking:ClientId, Matchmaking:ClientSecret y Matchmaking:Audience.");
            }

            using var response = await http.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["audience"] = audience
            }), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"No se obtuvo el token de servicio ({(int)response.StatusCode}).");
            }

            var data = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (data is null || string.IsNullOrWhiteSpace(data.AccessToken) || data.ExpiresIn <= 0)
            {
                throw new InvalidOperationException("El token de servicio recibido no es válido.");
            }

            // Se renueva un minuto antes de vencer (o a la mitad de su vida, si dura menos de dos minutos).
            var lifetime = TimeSpan.FromSeconds(data.ExpiresIn);
            var margin = lifetime > TimeSpan.FromMinutes(2) ? TimeSpan.FromMinutes(1) : lifetime / 2;
            cached = new CachedToken(data.AccessToken, clock.GetUtcNow() + lifetime - margin);
            return data.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }
}
