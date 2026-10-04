using System.Text.Json;
using System.Threading.Channels;
using BattleHub.Trivia.Api.Hubs;
using BattleHub.Trivia.Api.Matchmaking;
using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BattleHub.Trivia.IntegrationTests;

[Collection(TriviaDatabaseCollection.Name)]
public sealed class LiveHubTests(TriviaApiFactory factory)
{
    [Fact]
    public async Task TwoAuthenticatedClientsPlayTenRoundsPersistAndNotifyMatchmaking()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        var id = "live-" + Guid.NewGuid().ToString("N");
        await using var one = Connection("auth0|one");
        await using var two = Connection("auth0|two");
        var questions = Channel.CreateUnbounded<JsonElement>();
        var finished = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        one.On<JsonElement>("QuestionStarted", data => { questions.Writer.TryWrite(data); });
        one.On<JsonElement>("GameFinished", data => { finished.TrySetResult(data); });
        await one.StartAsync(ct); await two.StartAsync(ct);
        await one.InvokeAsync("JoinMatch", new JoinRequest(id, "room-token"), ct);
        await two.InvokeAsync("JoinMatch", new JoinRequest(id, "room-token"), ct);
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();
        var sessions = factory.Services.GetRequiredService<TriviaSessions>();
        for (var i = 0; i < 10; i++)
        {
            var question = await questions.Reader.ReadAsync(ct);
            Assert.False(question.TryGetProperty("correctAnswerId", out _));
            var questionId = question.GetProperty("questionId").GetString()!;
            var stored = (await repository.GetByIdAsync(questionId, ct))!;
            factory.Clock.Advance(3);
            await one.InvokeAsync("SubmitAnswer", new AnswerRequest(id, questionId, stored.CorrectAnswerId), ct);
            factory.Clock.Advance(1);
            await two.InvokeAsync("SubmitAnswer", new AnswerRequest(id, questionId, stored.Options.First(o => o.AnswerId != stored.CorrectAnswerId).AnswerId), ct);
            factory.Clock.Advance(5);
            await sessions.TickAsync(ct);
        }
        var result = await finished.Task.WaitAsync(ct);
        Assert.Equal("auth0|one", result.GetProperty("winnerUserId").GetString());
        var saved = await scope.ServiceProvider.GetRequiredService<IResultRepository>().GetByMatchIdAsync(id, ct);
        Assert.NotNull(saved);
        Assert.Equal(2200, saved.Players.Single(p => p.UserId == "auth0|one").Score);
        Assert.Equal(10, saved.Players.Single(p => p.UserId == "auth0|one").CorrectAnswers);
        var db = scope.ServiceProvider.GetRequiredService<TriviaDbContext>();
        Assert.False((await db.FinishNotifications.AsNoTracking().SingleAsync(p => p.MatchId == id, ct)).Delivered);
        var worker = new FinishWorker(factory.Services.GetRequiredService<IServiceScopeFactory>(),
            factory.Services.GetRequiredService<IMatchmakingGateway>(), factory.Clock, NullLogger<FinishWorker>.Instance);
        for (var i = 0; i < 5; i++) await worker.DispatchAsync(ct);
        Assert.True((await db.FinishNotifications.AsNoTracking().SingleAsync(p => p.MatchId == id, ct)).Delivered);
    }
    private HubConnection Connection(string user) => new HubConnectionBuilder().WithUrl("http://localhost/hubs/trivia", options =>
    {
        options.Transports = HttpTransportType.LongPolling;
        options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
        options.AccessTokenProvider = () => Task.FromResult<string?>(factory.Token(user, writer: false));
    }).Build();
}
