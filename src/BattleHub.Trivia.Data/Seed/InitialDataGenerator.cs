using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BattleHub.Trivia.Data.Seed;

/// <summary>
/// Genera el banco inicial de preguntas de demostración.
/// Es idempotente: si ya hay preguntas, no inserta nada.
/// Pensado para el proceso independiente de Docker Compose (DbInit), no para el arranque de la API.
/// </summary>
public sealed class InitialDataGenerator(TriviaDbContext db, ILogger<InitialDataGenerator> logger)
{
    /// <summary>Devuelve cuántas preguntas insertó (0 si la tabla ya tenía datos).</summary>
    public async Task<int> GenerateAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Questions.AnyAsync(cancellationToken))
        {
            logger.LogInformation(
                "El banco de preguntas ya tiene datos; no se insertan duplicados.");
            return 0;
        }

        var questions = QuestionBankData.Questions.Select(ToRecord).ToList();

        db.Questions.AddRange(questions);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Se insertaron {QuestionCount} preguntas y {OptionCount} opciones.",
            questions.Count,
            questions.Sum(question => question.Options.Count));

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
