
using Microsoft.AspNetCore.SignalR;

namespace BattleHub.Trivia.Api.Hubs;

// Partida de un solo jugador: modo práctica (desde el Shell) y modo playground (sin el Shell). No guarda resultados.
public sealed class PracticeHub(
    PracticeSessions sessions) : Hub
{
    public async Task<object> StartPractice(
        string displayName)
    {
        if (Context.Items.ContainsKey("practice"))
        {
            throw new HubException("PRACTICE_ALREADY_JOINED");
        }

        var (matchId, secret) = await sessions.CreateAsync(
            displayName,
            Context.ConnectionAborted);

        
        await sessions.AttachConnectionAsync(
            matchId,
            secret,
            Context.ConnectionId,
            Context.ConnectionAborted);

        Context.Items["practice"] = matchId;

        var firstQuestion = await sessions.StartAsync(
            matchId,
            secret,
            Context.ConnectionAborted);


        return new
        {
            matchId,
            secret,
            firstQuestion
        };
    }

    

public async Task<object> SubmitPracticeAnswer(
    string matchId,
    string secret,
    string questionId,
    string answerId)
{
    if (!Context.Items.TryGetValue("practice", out var currentMatch)
        || currentMatch is not string activeMatchId
        || activeMatchId != matchId)
    {
        throw new HubException("PRACTICE_NOT_JOINED");
    }

    return await sessions.AnswerAsync(
        matchId,
        secret,
        Context.ConnectionId,
        questionId,
        answerId,
        Context.ConnectionAborted);
}


public override async Task OnDisconnectedAsync(
    Exception? exception)
{
    if (Context.Items.TryGetValue(
            "practice",
            out var currentMatch) &&
        currentMatch is string matchId)
    {
        await sessions.DetachConnectionAsync(
            matchId,
            Context.ConnectionId);
    }

    await base.OnDisconnectedAsync(exception);
}

public async Task<object> ReconnectPractice(
    string matchId,
    string secret)
{
    if (Context.Items.ContainsKey("practice"))
    {
        throw new HubException("PRACTICE_ALREADY_JOINED");
    }

    await sessions.AttachConnectionAsync(
        matchId,
        secret,
        Context.ConnectionId,
        Context.ConnectionAborted);

    Context.Items["practice"] = matchId;

    return await sessions.GetStateAsync(
        matchId,
        secret,
        Context.ConnectionAborted);
}

}
