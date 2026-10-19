using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BattleHub.Trivia.IntegrationTests;

[Collection(TriviaDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class QuestionSeederTests(TriviaApiFactory factory)
{
    [Fact]
    public async Task SecondSeedDoesNotDuplicateAndOptionsPointToTheirQuestion()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TriviaDbContext>();

        // La fábrica ya cargó el banco al crear la base de prueba.
        Assert.Equal(30, await db.Questions.CountAsync());
        Assert.Equal(120, await db.AnswerOptions.CountAsync());

        var loaded = await scope.ServiceProvider.GetRequiredService<QuestionSeeder>().SeedAsync();

        Assert.Equal(0, loaded);
        Assert.Equal(30, await db.Questions.CountAsync());
        Assert.Equal(120, await db.AnswerOptions.CountAsync());

        // Cada opción apunta, por QuestionRefId, a la pregunta que tiene su mismo QuestionId.
        var mismatched = await db.AnswerOptions.CountAsync(option =>
            !db.Questions.Any(question =>
                question.Id == option.QuestionRefId && question.QuestionId == option.QuestionId));
        Assert.Equal(0, mismatched);
    }
}
