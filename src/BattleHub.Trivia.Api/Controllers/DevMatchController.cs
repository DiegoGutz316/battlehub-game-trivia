using BattleHub.Trivia.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BattleHub.Trivia.Api.Controllers;

[ApiController]
[Route("api/dev/matches")]
public sealed class DevMatchController : ControllerBase
{
    private readonly TriviaMatchBootstrapService _bootstrapService;
    private readonly TriviaMatchRunner _matchRunner;
    private readonly IWebHostEnvironment _environment;

    public DevMatchController(
        TriviaMatchBootstrapService bootstrapService,
        TriviaMatchRunner matchRunner,
        IWebHostEnvironment environment)
    {
        _bootstrapService = bootstrapService;
        _matchRunner = matchRunner;
        _environment = environment;
    }

    [HttpPost("{matchId}")]
    public async Task<IActionResult> PrepareMatch(
        string matchId,
        [FromBody] PrepareMatchRequest request,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (request.Players.Count == 0)
        {
            return BadRequest(
                "La partida debe tener al menos un jugador.");
        }

        var players = request.Players
            .Select(player => (
                player.UserId,
                player.DisplayName))
            .ToList();

        await _bootstrapService.PrepareMatchAsync(
            matchId,
            players,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{matchId}/start")]
    public async Task<IActionResult> StartMatch(
        string matchId)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        await _matchRunner.StartAsync(matchId);

        return NoContent();
    }
}

public sealed record PrepareMatchRequest(
    IReadOnlyList<PreparePlayerRequest> Players);

public sealed record PreparePlayerRequest(
    string UserId,
    string DisplayName);