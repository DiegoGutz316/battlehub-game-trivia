using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Trivia.Api.Profiles;

public interface IProfileGateway
{
    Task<bool> CanPlayAsync(string accessToken, CancellationToken ct);
}

// Los permisos de Profile no vienen en el token: se consultan con el token del jugador.
public sealed class ProfileGateway(HttpClient http, IConfiguration config) : IProfileGateway
{
    public const string PlayPermission = "games.trivia.play";

    private sealed record PermissionsResponse(string[]? Permissions);

    private Uri Url(string path)
    {
        var origin = config["Profile:BaseUrl"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https") || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            throw new InvalidOperationException("Configurar Profile:BaseUrl.");
        }

        return new Uri(uri.AbsoluteUri.TrimEnd('/') + path);
    }

    public async Task<bool> CanPlayAsync(string accessToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || accessToken.Length > 16000)
        {
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, Url("/api/profiles/me/permissions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            using var response = await http.SendAsync(request, ct);
            // 404: el jugador todavía no tiene perfil en Profile.
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HubException("PROFILE_UNAVAILABLE");
            }

            var body = await response.Content.ReadFromJsonAsync<PermissionsResponse>(cancellationToken: ct);
            return body?.Permissions?.Contains(PlayPermission, StringComparer.Ordinal) == true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        { throw new HubException("PROFILE_UNAVAILABLE"); }
    }
}
