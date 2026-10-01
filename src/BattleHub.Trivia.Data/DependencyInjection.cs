using BattleHub.Trivia.Data.Context;
using BattleHub.Trivia.Data.Repositories;
using BattleHub.Trivia.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BattleHub.Trivia.Data;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Trivia";

    public const string LocalFallbackConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=BattleHubTrivia;Trusted_Connection=True;TrustServerCertificate=True";

    public static IServiceCollection AddTriviaData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = LocalFallbackConnectionString;

        services.AddDbContext<TriviaDbContext>(options =>
            TriviaDbContext.ConfigureSqlServer(options, connectionString));

        services.AddScoped<IResultRepository, ResultRepository>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();

        return services;
    }
}
