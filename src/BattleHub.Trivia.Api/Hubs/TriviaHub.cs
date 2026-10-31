using BattleHub.Trivia.Api.Auth;
using BattleHub.Trivia.Api.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Trivia.Api.Hubs;

public sealed record RoomPlayer(string UserId, string DisplayName);
public sealed record MatchRoom(string Id, RoomPlayer[] Participants);
public sealed record JoinRequest(string MatchId, RoomPlayer[] Participants);
public sealed record AnswerRequest(string MatchId, string QuestionId, string AnswerId);

// El juego no llama a Matchmaking (R-46): el Shell abre la sala y le entrega al juego quiénes participan.
[Authorize(Policy = TriviaAuth.Play)]
public sealed class TriviaHub(TriviaSessions sessions, IProfileGateway profiles) : Hub
{
    private static MatchRoom ValidRoom(JoinRequest request, string userId)
    {
        if (string.IsNullOrWhiteSpace(request.MatchId) || request.MatchId.Length > 64) throw new HubException("MATCH_NOT_FOUND");
        var players = request.Participants;
        if (players is null || players.Length < 1 || players.Length > 20
            || players.Any(p => p is null || string.IsNullOrWhiteSpace(p.UserId) || p.UserId.Length > 64 || string.IsNullOrWhiteSpace(p.DisplayName) || p.DisplayName.Length > 100)
            || players.Select(p => p.UserId).Distinct(StringComparer.Ordinal).Count() != players.Length
            || !players.Any(p => p.UserId == userId))
            throw new HubException("PLAYER_NOT_IN_MATCH");
        return new MatchRoom(request.MatchId, players);
    }
    public async Task JoinMatch(JoinRequest request)
    {
        if (Context.Items.ContainsKey("room") && Context.Items["room"] as string != request.MatchId) throw new HubException("PLAYER_NOT_IN_MATCH");
        var id = TriviaAuth.UserId(Context.User!);
        if (!await profiles.CanPlayAsync(TriviaAuth.AccessToken(Context.GetHttpContext()), Context.ConnectionAborted))
            throw new HubException("PERMISSION_DENIED");
        var room = ValidRoom(request, id);
        await Groups.AddToGroupAsync(Context.ConnectionId, request.MatchId);
        try
        {
            await sessions.JoinAsync(room, id, Context.ConnectionId, Context.ConnectionAborted);
            Context.Items["room"] = request.MatchId;
        }
        catch
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, request.MatchId);
            await sessions.DisconnectAsync(request.MatchId, Context.ConnectionId);
            throw;
        }
    }
    public Task SubmitAnswer(AnswerRequest request)
    {
        if (!Context.Items.TryGetValue("room", out var room) || room as string != request.MatchId) throw new HubException("PLAYER_NOT_IN_MATCH");
        return sessions.AnswerAsync(request, TriviaAuth.UserId(Context.User!), Context.ConnectionId, Context.ConnectionAborted);
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("room", out var room) && room is string id) await sessions.DisconnectAsync(id, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
