using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BattleHub.Trivia.Data.Context;

public sealed class TriviaDbContextFactory : IDesignTimeDbContextFactory<TriviaDbContext>
{
    public TriviaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TriviaDbContext>();
        // Solo para dotnet ef. Generar una migración no abre la conexión; para aplicarla hay que definir la variable.
        TriviaDbContext.ConfigureSqlServer(
            options,
            Environment.GetEnvironmentVariable("ConnectionStrings__Trivia") ?? "Server=sin-configurar");

        return new TriviaDbContext(options.Options);
    }
}
