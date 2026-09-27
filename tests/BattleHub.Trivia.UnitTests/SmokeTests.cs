namespace BattleHub.Trivia.UnitTests;

// Prueba temporal para que el pipeline de CI tenga al menos una prueba con Category=Unit.
// Eliminarla cuando existan pruebas reales de la lógica del juego.
[Trait("Category", "Unit")]
public class SmokeTests
{
    [Fact]
    public void ProyectoDePruebasUnitarias_SeEjecuta()
    {
        Assert.True(true);
    }
}
