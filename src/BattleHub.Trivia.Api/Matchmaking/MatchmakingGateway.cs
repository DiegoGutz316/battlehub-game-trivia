using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Trivia.Api.Matchmaking;

public sealed record RoomPlayer(string UserId, string DisplayName);
public sealed record MatchRoom(string Id, string GameType, string Status, RoomPlayer[] Participants);
public enum FinishOutcome { Confirmed, Pending, Rejected }
public interface IMatchmakingGateway
{
    Task<MatchRoom> ValidateAsync(string matchId, string userId, string token, CancellationToken ct);
    Task<FinishOutcome> FinishAsync(string matchId, CancellationToken ct);
}

public sealed class MatchmakingGateway(
    HttpClient http,
    IConfiguration config,
    TimeProvider clock,
    ILogger<MatchmakingGateway> logger,
    IMatchmakingTokenProvider tokenProvider) : IMatchmakingGateway
{

    private Uri Url(string path)
    {
        var origin = config["Matchmaking:BaseUrl"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            throw new InvalidOperationException("Configurar Matchmaking:BaseUrl.");
        }

        return new Uri(uri.AbsoluteUri.TrimEnd('/') + path);
    }
    public async Task<MatchRoom> ValidateAsync(string matchId, string userId, string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(matchId) || matchId.Length > 64 || string.IsNullOrWhiteSpace(token) || token.Length > 16000)
        {
            throw new HubException("UNAUTHORIZED");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, Url("/api/matches/" + Uri.EscapeDataString(matchId)));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HubException("MATCH_NOT_FOUND");
        }

        var room = await response.Content.ReadFromJsonAsync<MatchRoom>(cancellationToken: ct);
        if (room is null || room.Id != matchId || room.GameType != "trivia" || room.Status != "Started")
        {
            throw new HubException("MATCH_NOT_FOUND");
        }

        if (room.Participants is null || room.Participants.Length < 1 || room.Participants.Length > 20
            || room.Participants.Any(p => p is null || string.IsNullOrWhiteSpace(p.UserId) || p.UserId.Length > 64 || string.IsNullOrWhiteSpace(p.DisplayName) || p.DisplayName.Length > 100)
            || room.Participants.Select(p => p.UserId).Distinct(StringComparer.Ordinal).Count() != room.Participants.Length
            || !room.Participants.Any(p => p.UserId == userId))
        {
            throw new HubException("PLAYER_NOT_IN_MATCH");
        }

        return room;
    }

    public async Task<FinishOutcome> FinishAsync(string matchId, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, Url("/api/matches/" + Uri.EscapeDataString(matchId) + "/finish"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokenProvider.GetTokenAsync(ct));
                using var response = await http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    return FinishOutcome.Confirmed;
                }

                if (response.StatusCode != HttpStatusCode.RequestTimeout && (int)response.StatusCode != 429 && (int)response.StatusCode < 500)
                {
                    logger.LogWarning("Matchmaking rechazó finalizar {MatchId}: {Status}", matchId, (int)response.StatusCode);
                    return FinishOutcome.Rejected;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
            { logger.LogWarning("No se confirmó la finalización de {MatchId}, intento {Attempt}", matchId, attempt); }
            if (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), clock, ct);
            }
        }
        return FinishOutcome.Pending;
    }
}
