-- Banco inicial de preguntas de Trivia Battle.
-- No forma parte de las migraciones: ejecutarlo a mano contra BattleHubTrivia
-- cuando haga falta cargar datos de ejemplo.

INSERT INTO Questions (QuestionId, Text, Category, Difficulty, CorrectAnswerId)
VALUES
    (N'q-001', N'¿Cuál es la capital de Costa Rica?', N'Geografía', N'facil', N'a'),
    (N'q-002', N'¿Cuántos jugadores como máximo pueden compartir una sala de BattleHub?', N'BattleHub', N'media', N'c'),

    -- Geografía
    (N'q-003', N'¿Cuál es el océano más grande del mundo?', N'Geografía', N'facil', N'b'),
    (N'q-004', N'¿En qué continente se encuentra Egipto?', N'Geografía', N'facil', N'c'),
    (N'q-005', N'¿Cuál es la capital de Argentina?', N'Geografía', N'facil', N'd'),
    (N'q-006', N'¿Cuál es el país más grande del mundo por superficie?', N'Geografía', N'media', N'a'),
    (N'q-007', N'¿Cuál es la capital de Canadá?', N'Geografía', N'media', N'c'),
    (N'q-008', N'¿Qué cordillera atraviesa varios países de América del Sur?', N'Geografía', N'media', N'b'),
    (N'q-009', N'¿Cuál es la capital de Australia?', N'Geografía', N'dificil', N'd'),
    (N'q-010', N'¿Qué país tiene como capital a Liubliana?', N'Geografía', N'dificil', N'a'),

    -- Ciencia
    (N'q-011', N'¿Cuál es el planeta más cercano al Sol?', N'Ciencia', N'facil', N'b'),
    (N'q-012', N'¿Qué gas necesitan los seres humanos para respirar?', N'Ciencia', N'facil', N'a'),
    (N'q-013', N'¿Cuál es el órgano que bombea la sangre por el cuerpo?', N'Ciencia', N'facil', N'c'),
    (N'q-014', N'¿Cuál es el símbolo químico del oro?', N'Ciencia', N'media', N'd'),
    (N'q-015', N'¿Cuántos planetas tiene el sistema solar?', N'Ciencia', N'media', N'b'),
    (N'q-016', N'¿Qué partícula subatómica tiene carga negativa?', N'Ciencia', N'media', N'c'),
    (N'q-017', N'¿Cuál es la unidad de medida de la resistencia eléctrica?', N'Ciencia', N'dificil', N'a'),
    (N'q-018', N'¿Qué elemento químico tiene el número atómico 6?', N'Ciencia', N'dificil', N'd'),
    (N'q-019', N'¿Cuál es la velocidad aproximada de la luz en el vacío?', N'Ciencia', N'dificil', N'b'),
    (N'q-020', N'¿Qué científico formuló las leyes del movimiento y la gravitación universal?', N'Ciencia', N'media', N'c'),

    -- Cultura general
    (N'q-021', N'¿Cuántos días tiene un año bisiesto?', N'Cultura general', N'facil', N'a'),
    (N'q-022', N'¿Cuál es el idioma oficial de Brasil?', N'Cultura general', N'facil', N'c'),
    (N'q-023', N'¿Cuántos lados tiene un hexágono?', N'Cultura general', N'facil', N'd'),
    (N'q-024', N'¿Quién pintó la Mona Lisa?', N'Cultura general', N'media', N'b'),
    (N'q-025', N'¿En qué país se originaron los Juegos Olímpicos antiguos?', N'Cultura general', N'media', N'a'),
    (N'q-026', N'¿Quién escribió Don Quijote de la Mancha?', N'Cultura general', N'media', N'c'),
    (N'q-027', N'¿En qué año llegó el ser humano por primera vez a la Luna?', N'Cultura general', N'dificil', N'd'),
    (N'q-028', N'¿Cuál es la moneda oficial de Japón?', N'Cultura general', N'facil', N'b'),
    (N'q-029', N'¿Quién compuso la Novena Sinfonía?', N'Cultura general', N'dificil', N'a'),
    (N'q-030', N'¿Cuál es la obra épica atribuida a Homero que narra el regreso de Odiseo?', N'Cultura general', N'dificil', N'c');

INSERT INTO AnswerOptions (QuestionId, AnswerId, Text)
VALUES
    (N'q-001', N'a', N'San José'),
    (N'q-001', N'b', N'Cartago'),
    (N'q-001', N'c', N'Alajuela'),
    (N'q-001', N'd', N'Heredia'),

    (N'q-002', N'a', N'2'),
    (N'q-002', N'b', N'4'),
    (N'q-002', N'c', N'10'),
    (N'q-002', N'd', N'20'),

    (N'q-003', N'a', N'Atlántico'),
    (N'q-003', N'b', N'Pacífico'),
    (N'q-003', N'c', N'Índico'),
    (N'q-003', N'd', N'Ártico'),

    (N'q-004', N'a', N'Europa'),
    (N'q-004', N'b', N'Asia'),
    (N'q-004', N'c', N'África'),
    (N'q-004', N'd', N'América'),

    (N'q-005', N'a', N'Córdoba'),
    (N'q-005', N'b', N'Rosario'),
    (N'q-005', N'c', N'Mendoza'),
    (N'q-005', N'd', N'Buenos Aires'),

    (N'q-006', N'a', N'Rusia'),
    (N'q-006', N'b', N'Canadá'),
    (N'q-006', N'c', N'China'),
    (N'q-006', N'd', N'Brasil'),

    (N'q-007', N'a', N'Toronto'),
    (N'q-007', N'b', N'Vancouver'),
    (N'q-007', N'c', N'Ottawa'),
    (N'q-007', N'd', N'Montreal'),

    (N'q-008', N'a', N'Alpes'),
    (N'q-008', N'b', N'Andes'),
    (N'q-008', N'c', N'Himalaya'),
    (N'q-008', N'd', N'Pirineos'),

    (N'q-009', N'a', N'Sídney'),
    (N'q-009', N'b', N'Melbourne'),
    (N'q-009', N'c', N'Perth'),
    (N'q-009', N'd', N'Canberra'),

    (N'q-010', N'a', N'Eslovenia'),
    (N'q-010', N'b', N'Eslovaquia'),
    (N'q-010', N'c', N'Croacia'),
    (N'q-010', N'd', N'Serbia'),

    (N'q-011', N'a', N'Venus'),
    (N'q-011', N'b', N'Mercurio'),
    (N'q-011', N'c', N'Marte'),
    (N'q-011', N'd', N'Tierra'),

    (N'q-012', N'a', N'Oxígeno'),
    (N'q-012', N'b', N'Helio'),
    (N'q-012', N'c', N'Hidrógeno'),
    (N'q-012', N'd', N'Neón'),

    (N'q-013', N'a', N'Pulmón'),
    (N'q-013', N'b', N'Hígado'),
    (N'q-013', N'c', N'Corazón'),
    (N'q-013', N'd', N'Riñón'),

    (N'q-014', N'a', N'Ag'),
    (N'q-014', N'b', N'Fe'),
    (N'q-014', N'c', N'O'),
    (N'q-014', N'd', N'Au'),

    (N'q-015', N'a', N'7'),
    (N'q-015', N'b', N'8'),
    (N'q-015', N'c', N'9'),
    (N'q-015', N'd', N'10'),

    (N'q-016', N'a', N'Protón'),
    (N'q-016', N'b', N'Neutrón'),
    (N'q-016', N'c', N'Electrón'),
    (N'q-016', N'd', N'Fotón'),

    (N'q-017', N'a', N'Ohmio'),
    (N'q-017', N'b', N'Voltio'),
    (N'q-017', N'c', N'Amperio'),
    (N'q-017', N'd', N'Vatio'),

    (N'q-018', N'a', N'Oxígeno'),
    (N'q-018', N'b', N'Nitrógeno'),
    (N'q-018', N'c', N'Helio'),
    (N'q-018', N'd', N'Carbono'),

    (N'q-019', N'a', N'30 000 km/s'),
    (N'q-019', N'b', N'300 000 km/s'),
    (N'q-019', N'c', N'3 000 km/s'),
    (N'q-019', N'd', N'3 000 000 km/s'),

    (N'q-020', N'a', N'Albert Einstein'),
    (N'q-020', N'b', N'Galileo Galilei'),
    (N'q-020', N'c', N'Isaac Newton'),
    (N'q-020', N'd', N'Nikola Tesla'),

    (N'q-021', N'a', N'366'),
    (N'q-021', N'b', N'365'),
    (N'q-021', N'c', N'364'),
    (N'q-021', N'd', N'367'),

    (N'q-022', N'a', N'Español'),
    (N'q-022', N'b', N'Francés'),
    (N'q-022', N'c', N'Portugués'),
    (N'q-022', N'd', N'Inglés'),

    (N'q-023', N'a', N'4'),
    (N'q-023', N'b', N'5'),
    (N'q-023', N'c', N'7'),
    (N'q-023', N'd', N'6'),

    (N'q-024', N'a', N'Pablo Picasso'),
    (N'q-024', N'b', N'Leonardo da Vinci'),
    (N'q-024', N'c', N'Vincent van Gogh'),
    (N'q-024', N'd', N'Claude Monet'),

    (N'q-025', N'a', N'Grecia'),
    (N'q-025', N'b', N'Italia'),
    (N'q-025', N'c', N'Egipto'),
    (N'q-025', N'd', N'Francia'),

    (N'q-026', N'a', N'Federico García Lorca'),
    (N'q-026', N'b', N'William Shakespeare'),
    (N'q-026', N'c', N'Miguel de Cervantes'),
    (N'q-026', N'd', N'Gabriel García Márquez'),

    (N'q-027', N'a', N'1959'),
    (N'q-027', N'b', N'1965'),
    (N'q-027', N'c', N'1972'),
    (N'q-027', N'd', N'1969'),

    (N'q-028', N'a', N'Won'),
    (N'q-028', N'b', N'Yen'),
    (N'q-028', N'c', N'Yuan'),
    (N'q-028', N'd', N'Dólar'),

    (N'q-029', N'a', N'Ludwig van Beethoven'),
    (N'q-029', N'b', N'Wolfgang Amadeus Mozart'),
    (N'q-029', N'c', N'Johann Sebastian Bach'),
    (N'q-029', N'd', N'Antonio Vivaldi'),

    (N'q-030', N'a', N'La Ilíada'),
    (N'q-030', N'b', N'La Eneida'),
    (N'q-030', N'c', N'La Odisea'),
    (N'q-030', N'd', N'Edipo Rey');