
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BattleHub.Trivia.Api.Hubs;

public sealed class PracticeWorker(
    PracticeSessions sessions,
    ILogger<PracticeWorker> logger)
    : BackgroundService
{
    
protected override async Task ExecuteAsync(
    CancellationToken stoppingToken)
{
    using var timer = new PeriodicTimer(
        TimeSpan.FromMilliseconds(250));

    var nextCleanup = DateTimeOffset.UtcNow.AddMinutes(1);

    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
        try
        {
            await sessions.TickAsync(stoppingToken);

            var now = DateTimeOffset.UtcNow;

            if (now >= nextCleanup)
            {
                await sessions.CleanupExpiredAsync(stoppingToken);

                nextCleanup = now.AddMinutes(1);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            break;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error en el procesamiento del modo práctica.");
        }
    }
}

}
