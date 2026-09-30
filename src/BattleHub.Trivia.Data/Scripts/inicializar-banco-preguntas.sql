-- Banco inicial de preguntas de Trivia Battle.
-- No forma parte de las migraciones: ejecutarlo a mano contra BattleHubTrivia
-- cuando haga falta cargar datos de ejemplo.

INSERT INTO Questions (QuestionId, Text, Category, Difficulty, CorrectAnswerId)
VALUES
    (N'q-001', N'¿Cuál es la capital de Costa Rica?', N'Geografía', N'facil', N'a'),
    (N'q-002', N'¿Cuántos jugadores como máximo pueden compartir una sala de BattleHub?', N'BattleHub', N'media', N'c');

INSERT INTO AnswerOptions (QuestionId, AnswerId, Text)
VALUES
    (N'q-001', N'a', N'San José'),
    (N'q-001', N'b', N'Cartago'),
    (N'q-001', N'c', N'Alajuela'),
    (N'q-001', N'd', N'Heredia'),
    (N'q-002', N'a', N'2'),
    (N'q-002', N'b', N'4'),
    (N'q-002', N'c', N'10'),
    (N'q-002', N'd', N'20');
