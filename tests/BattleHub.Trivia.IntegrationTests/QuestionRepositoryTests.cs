using BattleHub.Trivia.Data.Seed;
using BattleHub.Trivia.Domain.Entities;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BattleHub.Trivia.IntegrationTests;

[Collection(TriviaDatabaseCollection.Name)]
[Trait("Category", "Integration")]
public sealed class QuestionRepositoryTests(TriviaApiFactory factory)
{
    // Más que el banco completo: DrawAsync devuelve todas las preguntas que cumplan el filtro.
    private const int MoreThanTheBank = 100;

    [Fact]
    public async Task DrawAsync_FiltroDeCategoria_DevuelveTodasYSoloLasDeEsaCategoria()
    {
        var expectedIds = QuestionBankData.Questions
            .Where(question => question.Category == "Geografía")
            .Select(question => question.QuestionId)
            .ToHashSet();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();

        var questions = await repository.DrawAsync(
            MoreThanTheBank,
            new QuestionFilter(Category: "Geografía"));

        Assert.NotEmpty(expectedIds);
        Assert.True(expectedIds.Count < QuestionBankData.Questions.Count);
        Assert.Equal(expectedIds, questions.Select(question => question.QuestionId).ToHashSet());
        Assert.All(questions, question =>
        {
            Assert.Equal("Geografía", question.Category);
            Assert.Equal(new[] { "a", "b", "c", "d" }, question.Options.Select(option => option.AnswerId));
            Assert.Contains(question.Options, option => option.AnswerId == question.CorrectAnswerId);
        });
    }

    [Fact]
    public async Task DrawAsync_FiltroDeCategoriaYDificultad_AplicaLosDos()
    {
        var expectedIds = QuestionBankData.Questions
            .Where(question => question.Category == "Geografía" && question.Difficulty == "facil")
            .Select(question => question.QuestionId)
            .ToHashSet();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();

        var questions = await repository.DrawAsync(
            MoreThanTheBank,
            new QuestionFilter(Category: "Geografía", Difficulty: "facil"));

        Assert.NotEmpty(expectedIds);
        Assert.Equal(expectedIds, questions.Select(question => question.QuestionId).ToHashSet());
        Assert.All(questions, question =>
        {
            Assert.Equal("Geografía", question.Category);
            Assert.Equal("facil", question.Difficulty);
        });
    }

    [Fact]
    public async Task DrawAsync_FiltroDeCategoria_RespetaLaCantidadPedidaSinRepetir()
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();

        var questions = await repository.DrawAsync(2, new QuestionFilter(Category: "Geografía"));

        Assert.Equal(2, questions.Count);
        Assert.Equal(2, questions.Select(question => question.QuestionId).Distinct().Count());
        Assert.All(questions, question => Assert.Equal("Geografía", question.Category));
    }

    [Fact]
    public async Task DrawAsync_CategoriaInexistente_DevuelveListaVacia()
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();

        var questions = await repository.DrawAsync(
            MoreThanTheBank,
            new QuestionFilter(Category: "Categoría que no existe"));

        Assert.Empty(questions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DrawAsync_CantidadCeroONegativa_DevuelveListaVacia(int count)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();

        var questions = await repository.DrawAsync(count);

        Assert.Empty(questions);
    }
}
