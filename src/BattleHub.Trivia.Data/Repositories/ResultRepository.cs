using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Persistence;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Exceptions;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Trivia.Data.Repositories;

public sealed class ResultRepository(TriviaDbContext db) : IResultRepository
{
    public async Task SaveAsync(MatchResult result, CancellationToken cancellationToken = default)
    {
        var exists = await db.MatchResults
            .AnyAsync(match => match.MatchId == result.MatchId, cancellationToken);

        if (exists)
            throw new MatchAlreadyExistsException(result.MatchId);

        db.MatchResults.Add(ToRecord(result));
        db.FinishNotifications.Add(new FinishNotificationRecord { MatchId = result.MatchId });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            throw new MatchAlreadyExistsException(result.MatchId);
        }
    }

    public async Task<MatchResult?> GetByMatchIdAsync(string matchId, CancellationToken cancellationToken = default)
    {
        var record = await db.MatchResults
            .AsNoTracking()
            .Include(match => match.Players)
            .FirstOrDefaultAsync(match => match.MatchId == matchId, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<MatchResult>> GetHistoryByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var records = await db.MatchResults
            .AsNoTracking()
            .Include(match => match.Players)
            .Where(match => match.Players.Any(player => player.UserId == userId))
            .OrderByDescending(match => match.FinishedAt)
            .ThenByDescending(match => match.MatchId)
            .ToListAsync(cancellationToken);

        return records.Select(ToDomain).ToList();
    }

    public async Task<PlayerStats> GetStatsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var matches = await db.MatchResults
            .AsNoTracking()
            .Include(match => match.Players)
            .Where(match => match.Players.Any(player => player.UserId == userId))
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
        {
            return new PlayerStats
            {
                UserId = userId,
                MatchesPlayed = 0,
                Wins = 0,
                AverageScore = 0,
                BestScore = 0
            };
        }

        var scores = matches
            .Select(match => match.Players.First(player => player.UserId == userId).Score)
            .ToList();

        return new PlayerStats
        {
            UserId = userId,
            MatchesPlayed = matches.Count,
            Wins = matches.Count(match => !match.IsTie && match.WinnerUserId == userId),
            AverageScore = scores.Average(),
            BestScore = scores.Max()
        };
    }

    private static MatchResultRecord ToRecord(MatchResult result) => new()
    {
        MatchId = result.MatchId,
        GameType = result.GameType,
        StartedAt = result.StartedAt,
        FinishedAt = result.FinishedAt,
        WinnerUserId = result.WinnerUserId,
        IsTie = result.IsTie,
        DecidedByTiebreak = result.DecidedByTiebreak,
        MetadataJson = MetadataJson.Serialize(result.Metadata),
        Players = result.Players.Select(player => new PlayerResultRecord
        {
            MatchId = result.MatchId,
            UserId = player.UserId,
            DisplayName = player.DisplayName,
            Score = player.Score,
            CorrectAnswers = player.CorrectAnswers,
            CorrectAnswersTimeMs = player.CorrectAnswersTimeMs,
            Position = player.Position
        }).ToList()
    };

    private static MatchResult ToDomain(MatchResultRecord record) => new()
    {
        MatchId = record.MatchId,
        GameType = record.GameType,
        StartedAt = record.StartedAt,
        FinishedAt = record.FinishedAt,
        WinnerUserId = record.WinnerUserId,
        IsTie = record.IsTie,
        DecidedByTiebreak = record.DecidedByTiebreak,
        Metadata = MetadataJson.Deserialize(record.MetadataJson),
        Players = record.Players
            .OrderBy(player => player.Position)
            .ThenBy(player => player.UserId)
            .Select(player => new PlayerResult
            {
                UserId = player.UserId,
                DisplayName = player.DisplayName,
                Score = player.Score,
                CorrectAnswers = player.CorrectAnswers,
                CorrectAnswersTimeMs = player.CorrectAnswersTimeMs,
                Position = player.Position
            })
            .ToList()
    };

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException sql && sql.Number is 2601 or 2627;
}
