using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Repositories;
using BattleHub.Trivia.Data.Seed;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BattleHub.Trivia.Data;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Trivia";

    public static IServiceCollection AddTriviaData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configurar ConnectionStrings__Trivia.");
        }

        services.AddDbContext<TriviaDbContext>(options =>
            TriviaDbContext.ConfigureSqlServer(options, connectionString));

        services.AddScoped<IResultRepository, ResultRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<InitialDataGenerator>();
        services.AddScoped<QuestionSeeder>();

        return services;
    }
}
