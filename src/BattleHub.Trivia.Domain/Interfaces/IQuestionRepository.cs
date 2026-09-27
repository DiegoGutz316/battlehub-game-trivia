using BattleHub.Trivia.Domain.Entities;

namespace BattleHub.Trivia.Domain.Interfaces;

/// <summary>
/// Acceso de solo lectura al banco de preguntas.
/// La carga inicial vive en un script aparte, no en las migraciones.
/// </summary>
public interface IQuestionRepository
{
    /// <summary>Devuelve null si la pregunta no existe. Incluye la respuesta correcta.</summary>
    Task<Question?> GetByIdAsync(string questionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Selecciona preguntas distintas al azar.
    /// Si el banco no alcanza, devuelve las que haya. El filtro es opcional por categoría y dificultad.
    /// </summary>
    Task<IReadOnlyList<Question>> DrawAsync(
        int count,
        QuestionFilter? filter = null,
        CancellationToken cancellationToken = default);
}
