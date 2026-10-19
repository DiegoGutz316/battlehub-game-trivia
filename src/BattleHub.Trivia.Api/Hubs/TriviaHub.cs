using BattleHub.Trivia.Api.Matchmaking;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Trivia.Api.Hubs;

public sealed record JoinRequest(string MatchId, string UserId);
public sealed record AnswerRequest(string MatchId, string QuestionId, string AnswerId);

public sealed class TriviaHub(TriviaSessions sessions, IMatchmakingGateway matchmaking) : Hub
{
    public async Task JoinMatch(JoinRequest request)
    {
        if (Context.Items.ContainsKey("room") && Context.Items["room"] as string != request.MatchId) throw new HubException("PLAYER_NOT_IN_MATCH");
        if (Context.Items.TryGetValue("user", out var joined) && joined as string != request.UserId) throw new HubException("PLAYER_NOT_IN_MATCH");
        if (string.IsNullOrWhiteSpace(request.UserId) || request.UserId.Length > 64) throw new HubException("PLAYER_NOT_IN_MATCH");
        var room = await matchmaking.ValidateAsync(request.MatchId, request.UserId, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, request.MatchId);
        try
        {
            await sessions.JoinAsync(room, request.UserId, Context.ConnectionId, Context.ConnectionAborted);
            Context.Items["room"] = request.MatchId;
            Context.Items["user"] = request.UserId;
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
        if (!Context.Items.TryGetValue("room", out var room) || room as string != request.MatchId
            || !Context.Items.TryGetValue("user", out var user) || user is not string id) throw new HubException("PLAYER_NOT_IN_MATCH");
        return sessions.AnswerAsync(request, id, Context.ConnectionId, Context.ConnectionAborted);
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("room", out var room) && room is string id) await sessions.DisconnectAsync(id, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
