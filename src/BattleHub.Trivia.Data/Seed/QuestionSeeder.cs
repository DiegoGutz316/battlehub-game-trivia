namespace BattleHub.Trivia.Data.Seed;

/// <summary>
/// Fachada usada por las pruebas de integración.
/// La carga en Docker Compose la hace el proceso DbInit con <see cref="InitialDataGenerator"/>.
/// </summary>
public sealed class QuestionSeeder(InitialDataGenerator generator)
{
    /// <summary>Devuelve cuántas preguntas cargó (0 si ya había preguntas).</summary>
    public Task<int> SeedAsync(CancellationToken cancellationToken = default) =>
        generator.GenerateAsync(cancellationToken);
}
