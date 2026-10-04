using BattleHub.Trivia.Api.Matchmaking;
using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BattleHub.Trivia.IntegrationTests;

[Collection(TriviaDatabaseCollection.Name)]
public sealed class FinishWorkerTests(TriviaApiFactory factory)
{
    private sealed class Gateway(string target, FinishOutcome outcome) : IMatchmakingGateway
    {
        public int Calls;
        public Task<MatchRoom> ValidateAsync(string id, string user, string token, CancellationToken ct) => throw new NotSupportedException();
        public Task<FinishOutcome> FinishAsync(string id, CancellationToken ct)
        {
            if (id == target) { Calls++; return Task.FromResult(outcome); }
            return Task.FromResult(FinishOutcome.Confirmed);
        }
    }

    [Theory]
    [InlineData(FinishOutcome.Pending, false)]
    [InlineData(FinishOutcome.Rejected, true)]
    public async Task QueueSurvivesWorkerRecreationAndDoesNotRetryBlocked(FinishOutcome outcome, bool blocked)
    {
        var id = "retry-" + Guid.NewGuid().ToString("N");
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IResultRepository>().SaveAsync(new MatchResult {
            MatchId = id, StartedAt = factory.Clock.GetUtcNow(), FinishedAt = factory.Clock.GetUtcNow(),
            Players = [new PlayerResult { UserId = "auth0|one", DisplayName = "Uno", Position = 1 }], WinnerUserId = "auth0|one"
        });
        var gateway = new Gateway(id, outcome);
        var worker = CreateWorker(gateway);
        for (var i = 0; i < 6 && gateway.Calls == 0; i++) await worker.DispatchAsync(default);
        Assert.Equal(1, gateway.Calls);
        var db = scope.ServiceProvider.GetRequiredService<TriviaDbContext>();
        var pending = await db.FinishNotifications.AsNoTracking().SingleAsync(item => item.MatchId == id);
        Assert.Equal(blocked, pending.Blocked);
        Assert.False(pending.Delivered);
        factory.Clock.Advance(31);
        var restoredGateway = new Gateway(id, FinishOutcome.Confirmed);
        var restored = CreateWorker(restoredGateway);
        for (var i = 0; i < 6; i++) await restored.DispatchAsync(default);
        Assert.Equal(blocked ? 0 : 1, restoredGateway.Calls);
        Assert.Equal(!blocked, (await db.FinishNotifications.AsNoTracking().SingleAsync(item => item.MatchId == id)).Delivered);
    }

    private FinishWorker CreateWorker(IMatchmakingGateway gateway) => new(factory.Services.GetRequiredService<IServiceScopeFactory>(),
        gateway, factory.Clock, NullLogger<FinishWorker>.Instance);
}