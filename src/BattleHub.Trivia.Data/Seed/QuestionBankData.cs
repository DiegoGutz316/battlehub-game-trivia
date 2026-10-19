namespace BattleHub.Trivia.Data.Seed;

/// <summary>
/// Banco inicial de preguntas de Trivia Battle: 30 preguntas con 4 opciones cada una.
/// Lo carga <see cref="QuestionSeeder"/> cuando la tabla Questions está vacía.
/// </summary>
public static class QuestionBankData
{
    public static IReadOnlyList<SeedQuestion> Questions { get; } =
    [
        new("q-001", "¿Cuál es la capital de Costa Rica?", "Geografía", "facil", "a",
        [
            new("a", "San José"),
            new("b", "Cartago"),
            new("c", "Alajuela"),
            new("d", "Heredia"),
        ]),
        new("q-002", "¿Cuántos jugadores como máximo pueden compartir una sala de BattleHub?", "BattleHub", "media", "c",
        [
            new("a", "2"),
            new("b", "4"),
            new("c", "10"),
            new("d", "20"),
        ]),

        // Geografía
        new("q-003", "¿Cuál es el océano más grande del mundo?", "Geografía", "facil", "b",
        [
            new("a", "Atlántico"),
            new("b", "Pacífico"),
            new("c", "Índico"),
            new("d", "Ártico"),
        ]),
        new("q-004", "¿En qué continente se encuentra Egipto?", "Geografía", "facil", "c",
        [
            new("a", "Europa"),
            new("b", "Asia"),
            new("c", "África"),
            new("d", "América"),
        ]),
        new("q-005", "¿Cuál es la capital de Argentina?", "Geografía", "facil", "d",
        [
            new("a", "Córdoba"),
            new("b", "Rosario"),
            new("c", "Mendoza"),
            new("d", "Buenos Aires"),
        ]),
        new("q-006", "¿Cuál es el país más grande del mundo por superficie?", "Geografía", "media", "a",
        [
            new("a", "Rusia"),
            new("b", "Canadá"),
            new("c", "China"),
            new("d", "Brasil"),
        ]),
        new("q-007", "¿Cuál es la capital de Canadá?", "Geografía", "media", "c",
        [
            new("a", "Toronto"),
            new("b", "Vancouver"),
            new("c", "Ottawa"),
            new("d", "Montreal"),
        ]),
        new("q-008", "¿Qué cordillera atraviesa varios países de América del Sur?", "Geografía", "media", "b",
        [
            new("a", "Alpes"),
            new("b", "Andes"),
            new("c", "Himalaya"),
            new("d", "Pirineos"),
        ]),
        new("q-009", "¿Cuál es la capital de Australia?", "Geografía", "dificil", "d",
        [
            new("a", "Sídney"),
            new("b", "Melbourne"),
            new("c", "Perth"),
            new("d", "Canberra"),
        ]),
        new("q-010", "¿Qué país tiene como capital a Liubliana?", "Geografía", "dificil", "a",
        [
            new("a", "Eslovenia"),
            new("b", "Eslovaquia"),
            new("c", "Croacia"),
            new("d", "Serbia"),
        ]),

        // Ciencia
        new("q-011", "¿Cuál es el planeta más cercano al Sol?", "Ciencia", "facil", "b",
        [
            new("a", "Venus"),
            new("b", "Mercurio"),
            new("c", "Marte"),
            new("d", "Tierra"),
        ]),
        new("q-012", "¿Qué gas necesitan los seres humanos para respirar?", "Ciencia", "facil", "a",
        [
            new("a", "Oxígeno"),
            new("b", "Helio"),
            new("c", "Hidrógeno"),
            new("d", "Neón"),
        ]),
        new("q-013", "¿Cuál es el órgano que bombea la sangre por el cuerpo?", "Ciencia", "facil", "c",
        [
            new("a", "Pulmón"),
            new("b", "Hígado"),
            new("c", "Corazón"),
            new("d", "Riñón"),
        ]),
        new("q-014", "¿Cuál es el símbolo químico del oro?", "Ciencia", "media", "d",
        [
            new("a", "Ag"),
            new("b", "Fe"),
            new("c", "O"),
            new("d", "Au"),
        ]),
        new("q-015", "¿Cuántos planetas tiene el sistema solar?", "Ciencia", "media", "b",
        [
            new("a", "7"),
            new("b", "8"),
            new("c", "9"),
            new("d", "10"),
        ]),
        new("q-016", "¿Qué partícula subatómica tiene carga negativa?", "Ciencia", "media", "c",
        [
            new("a", "Protón"),
            new("b", "Neutrón"),
            new("c", "Electrón"),
            new("d", "Fotón"),
        ]),
        new("q-017", "¿Cuál es la unidad de medida de la resistencia eléctrica?", "Ciencia", "dificil", "a",
        [
            new("a", "Ohmio"),
            new("b", "Voltio"),
            new("c", "Amperio"),
            new("d", "Vatio"),
        ]),
        new("q-018", "¿Qué elemento químico tiene el número atómico 6?", "Ciencia", "dificil", "d",
        [
            new("a", "Oxígeno"),
            new("b", "Nitrógeno"),
            new("c", "Helio"),
            new("d", "Carbono"),
        ]),
        new("q-019", "¿Cuál es la velocidad aproximada de la luz en el vacío?", "Ciencia", "dificil", "b",
        [
            new("a", "30 000 km/s"),
            new("b", "300 000 km/s"),
            new("c", "3 000 km/s"),
            new("d", "3 000 000 km/s"),
        ]),
        new("q-020", "¿Qué científico formuló las leyes del movimiento y la gravitación universal?", "Ciencia", "media", "c",
        [
            new("a", "Albert Einstein"),
            new("b", "Galileo Galilei"),
            new("c", "Isaac Newton"),
            new("d", "Nikola Tesla"),
        ]),

        // Cultura general
        new("q-021", "¿Cuántos días tiene un año bisiesto?", "Cultura general", "facil", "a",
        [
            new("a", "366"),
            new("b", "365"),
            new("c", "364"),
            new("d", "367"),
        ]),
        new("q-022", "¿Cuál es el idioma oficial de Brasil?", "Cultura general", "facil", "c",
        [
            new("a", "Español"),
            new("b", "Francés"),
            new("c", "Portugués"),
            new("d", "Inglés"),
        ]),
        new("q-023", "¿Cuántos lados tiene un hexágono?", "Cultura general", "facil", "d",
        [
            new("a", "4"),
            new("b", "5"),
            new("c", "7"),
            new("d", "6"),
        ]),
        new("q-024", "¿Quién pintó la Mona Lisa?", "Cultura general", "media", "b",
        [
            new("a", "Pablo Picasso"),
            new("b", "Leonardo da Vinci"),
            new("c", "Vincent van Gogh"),
            new("d", "Claude Monet"),
        ]),
        new("q-025", "¿En qué país se originaron los Juegos Olímpicos antiguos?", "Cultura general", "media", "a",
        [
            new("a", "Grecia"),
            new("b", "Italia"),
            new("c", "Egipto"),
            new("d", "Francia"),
        ]),
        new("q-026", "¿Quién escribió Don Quijote de la Mancha?", "Cultura general", "media", "c",
        [
            new("a", "Federico García Lorca"),
            new("b", "William Shakespeare"),
            new("c", "Miguel de Cervantes"),
            new("d", "Gabriel García Márquez"),
        ]),
        new("q-027", "¿En qué año llegó el ser humano por primera vez a la Luna?", "Cultura general", "dificil", "d",
        [
            new("a", "1959"),
            new("b", "1965"),
            new("c", "1972"),
            new("d", "1969"),
        ]),
        new("q-028", "¿Cuál es la moneda oficial de Japón?", "Cultura general", "facil", "b",
        [
            new("a", "Won"),
            new("b", "Yen"),
            new("c", "Yuan"),
            new("d", "Dólar"),
        ]),
        new("q-029", "¿Quién compuso la Novena Sinfonía?", "Cultura general", "dificil", "a",
        [
            new("a", "Ludwig van Beethoven"),
            new("b", "Wolfgang Amadeus Mozart"),
            new("c", "Johann Sebastian Bach"),
            new("d", "Antonio Vivaldi"),
        ]),
        new("q-030", "¿Cuál es la obra épica atribuida a Homero que narra el regreso de Odiseo?", "Cultura general", "dificil", "c",
        [
            new("a", "La Ilíada"),
            new("b", "La Eneida"),
            new("c", "La Odisea"),
            new("d", "Edipo Rey"),
        ]),
    ];
}

public sealed record SeedQuestion(
    string QuestionId,
    string Text,
    string? Category,
    string? Difficulty,
    string CorrectAnswerId,
    IReadOnlyList<SeedOption> Options);

public sealed record SeedOption(string AnswerId, string Text);
