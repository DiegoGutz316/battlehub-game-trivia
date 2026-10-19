using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BattleHub.Trivia.Data.Seed;

/// <summary>
/// Carga el banco inicial de preguntas. No hace nada si la tabla Questions ya tiene datos.
/// </summary>
public sealed class QuestionSeeder(TriviaDbContext db)
{
    /// <summary>Devuelve cuántas preguntas cargó (0 si ya había preguntas).</summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Questions.AnyAsync(cancellationToken))
            return 0;

        var questions = QuestionBankData.Questions.Select(ToRecord).ToList();

        // Un solo SaveChanges: entran todas las filas o ninguna.
        // EF genera los Guid al agregar las entidades y llena QuestionRefId por la navegación Options.
        db.Questions.AddRange(questions);
        await db.SaveChangesAsync(cancellationToken);

        return questions.Count;
    }

    private static QuestionRecord ToRecord(SeedQuestion question) => new()
    {
        QuestionId = question.QuestionId,
        Text = question.Text,
        Category = question.Category,
        Difficulty = question.Difficulty,
        CorrectAnswerId = question.CorrectAnswerId,
        Options = question.Options
            .Select(option => new AnswerOptionRecord
            {
                QuestionId = question.QuestionId,
                AnswerId = option.AnswerId,
                Text = option.Text
            })
            .ToList()
    };
}
