using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BattleHub.Trivia.Data.Context;

public sealed class TriviaDbContextFactory : IDesignTimeDbContextFactory<TriviaDbContext>
{
    public TriviaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TriviaDbContext>();
        // Solo para generar migraciones. En ejecución la cadena sale de user-secrets.
        TriviaDbContext.ConfigureSqlServer(
            options,
            "Server=(localdb)\\mssqllocaldb;Database=BattleHubTrivia;Trusted_Connection=True;TrustServerCertificate=True");

        return new TriviaDbContext(options.Options);
    }
}
