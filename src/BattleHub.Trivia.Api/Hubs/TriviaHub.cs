using BattleHub.Trivia.Api.Auth;
using BattleHub.Trivia.Api.Matchmaking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Trivia.Api.Hubs;

public sealed record JoinRequest(string MatchId, string MatchmakingAccessToken);
public sealed record AnswerRequest(string MatchId, string QuestionId, string AnswerId);

[Authorize(Policy = TriviaAuth.Play)]
public sealed class TriviaHub(TriviaSessions sessions, IMatchmakingGateway matchmaking) : Hub
{
    public async Task JoinMatch(JoinRequest request)
    {
        if (Context.Items.ContainsKey("room") && Context.Items["room"] as string != request.MatchId) throw new HubException("PLAYER_NOT_IN_MATCH");
        var id = TriviaAuth.UserId(Context.User!);
        var room = await matchmaking.ValidateAsync(request.MatchId, id, request.MatchmakingAccessToken, Context.ConnectionAborted);
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
