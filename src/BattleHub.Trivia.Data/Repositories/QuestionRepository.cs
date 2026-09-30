using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Persistence;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Trivia.Data.Repositories;

public sealed class QuestionRepository(TriviaDbContext db) : IQuestionRepository
{
    public async Task<Question?> GetByIdAsync(string questionId, CancellationToken cancellationToken = default)
    {
        var record = await db.Questions
            .AsNoTracking()
            .Include(question => question.Options)
            .FirstOrDefaultAsync(question => question.QuestionId == questionId, cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<Question>> DrawAsync(
        int count,
        QuestionFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            return [];

        IQueryable<QuestionRecord> query = db.Questions
            .AsNoTracking()
            .Include(question => question.Options);

        if (!string.IsNullOrWhiteSpace(filter?.Category))
            query = query.Where(question => question.Category == filter.Category);

        if (!string.IsNullOrWhiteSpace(filter?.Difficulty))
            query = query.Where(question => question.Difficulty == filter.Difficulty);

        var records = await query.ToListAsync(cancellationToken);

        return records
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .Select(ToDomain)
            .ToList();
    }

    private static Question ToDomain(QuestionRecord record) => new()
    {
        QuestionId = record.QuestionId,
        Text = record.Text,
        Category = record.Category,
        Difficulty = record.Difficulty,
        CorrectAnswerId = record.CorrectAnswerId,
        Options = record.Options
            .OrderBy(option => option.AnswerId)
            .Select(option => new AnswerOption
            {
                AnswerId = option.AnswerId,
                Text = option.Text
            })
            .ToList()
    };
}
