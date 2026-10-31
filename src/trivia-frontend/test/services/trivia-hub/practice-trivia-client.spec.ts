import { PracticeTriviaClient } from '../../../src/services/trivia-hub/practice-trivia-client';
import type { HubConnectionState } from '../../../src/services/trivia-hub/trivia-hub-client';
import type { QuestionStartedEvent } from '../../../src/services/trivia-hub/contract';

type Callback = () => void;
const mockLifecycle: { reconnecting?: Callback; reconnected?: Callback; close?: Callback } = {};
const mockServerEvents = new Map<string, (data: unknown) => void>();
const mockConnection = {
  start: jest.fn(async () => undefined),
  stop: jest.fn(async () => undefined),
  invoke: jest.fn<Promise<unknown>, unknown[]>(async () => undefined),
  on: jest.fn((name: string, handler: (data: unknown) => void) => { mockServerEvents.set(name, handler); }),
  onreconnecting: jest.fn((callback: Callback) => { mockLifecycle.reconnecting = callback; }),
  onreconnected: jest.fn((callback: Callback) => { mockLifecycle.reconnected = callback; }),
  onclose: jest.fn((callback: Callback) => { mockLifecycle.close = callback; }),
};
const mockBuilder = {
  withUrl: jest.fn(() => mockBuilder),
  withAutomaticReconnect: jest.fn(() => mockBuilder),
  configureLogging: jest.fn(() => mockBuilder),
  build: jest.fn(() => mockConnection),
};
jest.mock('@microsoft/signalr', () => ({ HubConnectionBuilder: jest.fn(() => mockBuilder), LogLevel: { None: 6 } }));

const player = { id: 'user-dev-1', displayName: 'Jugador 1' };
const now = '2026-10-25T18:00:00.000Z';
const question = (id = 'q1', number = 1): QuestionStartedEvent => ({
  questionId: id, questionNumber: number, totalQuestions: 10, text: '¿Capital de Costa Rica?', category: 'Geografía',
  options: [{ answerId: 'a', text: 'San José' }, { answerId: 'b', text: 'Cartago' }],
  expiresAtUtc: '2026-10-25T18:00:15.000Z', serverTimeUtc: now,
});
const started = { matchId: 'practice-1', secret: 'ABCDEF', firstQuestion: question() };
const server = (event: string, data: unknown): void => mockServerEvents.get(event)?.(data);

async function startedClient() {
  const client = new PracticeTriviaClient('http://api', player);
  const events: { name: string; data: unknown }[] = [];
  for (const name of ['CurrentState', 'QuestionStarted', 'AnswerReceived', 'QuestionClosed', 'ScoreboardUpdated', 'GameFinished'] as const)
    client.on(name, data => events.push({ name, data }));
  mockConnection.invoke.mockResolvedValueOnce(started);
  await client.connect();
  await client.joinMatch();
  return { client, events };
}

beforeEach(() => { jest.clearAllMocks(); mockServerEvents.clear(); });

describe('PracticeTriviaClient', () => {
  it('se conecta al hub de práctica, sin token y con reconexión automática', () => {
    new PracticeTriviaClient('http://localhost:5185', player);

    expect(mockBuilder.withUrl).toHaveBeenCalledWith('http://localhost:5185/hubs/trivia/practice');
    expect(mockBuilder.withAutomaticReconnect).toHaveBeenCalledWith([0, 2000, 5000, 10000]);
    expect(mockBuilder.configureLogging).toHaveBeenCalledWith(6);
  });

  it('informa los estados de la conexión y deja de avisar al cancelar o desconectar', async () => {
    const client = new PracticeTriviaClient('http://api', player);
    const states: HubConnectionState[] = [];
    const cancelled = jest.fn();
    client.onConnectionStateChanged(state => states.push(state));
    client.onConnectionStateChanged(cancelled)();

    await client.connect();
    mockLifecycle.reconnecting?.();
    mockLifecycle.reconnected?.();
    await client.disconnect();
    mockLifecycle.close?.();

    expect(states).toEqual(['connecting', 'connected', 'reconnecting', 'connected']);
    expect(cancelled).not.toHaveBeenCalled();
    expect(mockConnection.stop).toHaveBeenCalledTimes(1);
  });

  it('si la API no responde, connect() queda en disconnected y lanza un error legible', async () => {
    mockConnection.start.mockRejectedValueOnce(new Error('ECONNREFUSED'));
    const client = new PracticeTriviaClient('http://api', player);

    await expect(client.connect()).rejects.toThrow('No se pudo conectar con Trivia');
    expect(client.connectionState).toBe('disconnected');
  });

  it('joinMatch() empieza la partida con el nombre del jugador y entrega la primera pregunta', async () => {
    const { events } = await startedClient();

    expect(mockConnection.invoke).toHaveBeenCalledWith('StartPractice', 'Jugador 1');
    expect(events).toEqual([{ name: 'CurrentState', data: {
      matchId: 'practice-1', phase: 'playing', serverTimeUtc: now, totalQuestions: 10,
      players: [{ userId: 'user-dev-1', displayName: 'Jugador 1', score: 0, isConnected: true }],
      currentQuestion: { ...question(), hasAnswered: false, selectedAnswerId: null }, lastQuestionResult: null,
    } }]);
  });

  it('recorta el nombre al máximo que acepta el hub', async () => {
    const client = new PracticeTriviaClient('http://api', { id: 'one', displayName: 'n'.repeat(80) });
    mockConnection.invoke.mockResolvedValueOnce(started);

    await client.joinMatch();

    expect(mockConnection.invoke).toHaveBeenCalledWith('StartPractice', 'n'.repeat(50));
  });

  it('si el servidor no puede empezar la partida, lanza un error legible', async () => {
    const client = new PracticeTriviaClient('http://api', player);
    mockConnection.invoke.mockRejectedValueOnce(new Error('HubException: INSUFFICIENT_QUESTIONS'));

    await expect(client.joinMatch()).rejects.toThrow('No se pudo iniciar la partida');
  });

  it('envía la respuesta con la clave de la partida y la confirma como AnswerReceived', async () => {
    const { client, events } = await startedClient();
    mockConnection.invoke.mockResolvedValueOnce({ questionId: 'q1', selectedAnswerId: 'a', pointsEarned: 230, totalScore: 230, receivedAtUtc: now });

    await client.submitAnswer({ matchId: 'ignorado', questionId: 'q1', answerId: 'a' });

    expect(mockConnection.invoke).toHaveBeenLastCalledWith('SubmitPracticeAnswer', 'practice-1', 'ABCDEF', 'q1', 'a');
    expect(events.at(-1)).toEqual({ name: 'AnswerReceived', data: { questionId: 'q1', answerId: 'a', receivedAtUtc: now } });
  });

  it('no envía respuestas antes de empezar la partida', async () => {
    const client = new PracticeTriviaClient('http://api', player);

    await expect(client.submitAnswer({ matchId: 'm', questionId: 'q1', answerId: 'a' })).rejects.toThrow('todavía no empezó');
    expect(mockConnection.invoke).not.toHaveBeenCalled();
  });

  it('reenvía las preguntas y la revisión, y actualiza el marcador con el puntaje total', async () => {
    const { events } = await startedClient();
    const closed = { questionId: 'q1', correctAnswerId: 'a', selectedAnswerId: 'a', isCorrect: true, pointsEarned: 230, totalScore: 230, nextQuestionAtUtc: now, serverTimeUtc: now };

    server('QuestionClosed', closed);
    server('QuestionStarted', question('q2', 2));

    expect(events.slice(1)).toEqual([
      { name: 'QuestionClosed', data: closed },
      { name: 'ScoreboardUpdated', data: { players: [{ userId: 'user-dev-1', displayName: 'Jugador 1', score: 230, isConnected: true }] } },
      { name: 'QuestionStarted', data: question('q2', 2) },
    ]);
  });

  it('convierte el fin de la partida en un resultado de un solo jugador', async () => {
    const { events } = await startedClient();

    server('GameFinished', { matchId: 'practice-1', totalQuestions: 10, totalScore: 1800, correctAnswers: 8 });

    expect(events.at(-1)).toEqual({ name: 'GameFinished', data: expect.objectContaining({
      matchId: 'practice-1', winnerUserId: 'user-dev-1', isTie: false, decidedByTiebreak: false,
      players: [{ userId: 'user-dev-1', displayName: 'Jugador 1', score: 1800, correctAnswers: 8, correctAnswersTimeMs: 0, position: 1 }],
    }) });
  });

  it('después de reconectar recupera la partida con su clave, incluida la respuesta ya enviada', async () => {
    const { client, events } = await startedClient();
    mockConnection.invoke.mockResolvedValueOnce({ questionId: 'q1', selectedAnswerId: 'b', pointsEarned: 0, totalScore: 0, receivedAtUtc: now });
    await client.submitAnswer({ matchId: 'm', questionId: 'q1', answerId: 'b' });
    mockConnection.invoke.mockResolvedValueOnce({
      matchId: 'practice-1', phase: 'playing', questionNumber: 1, totalQuestions: 10, totalScore: 0, correctAnswers: 0,
      currentQuestion: question(), review: null, serverTimeUtc: now,
    });

    await client.joinMatch();

    expect(mockConnection.invoke).toHaveBeenLastCalledWith('ReconnectPractice', 'practice-1', 'ABCDEF');
    expect(events.at(-1)).toEqual({ name: 'CurrentState', data: expect.objectContaining({
      phase: 'playing', currentQuestion: { ...question(), hasAnswered: true, selectedAnswerId: 'b' }, lastQuestionResult: null,
    }) });
  });

  it('al reconectar durante la revisión conserva la última pregunta mostrada', async () => {
    const { client, events } = await startedClient();
    const review = { questionId: 'q1', correctAnswerId: 'a', selectedAnswerId: null, isCorrect: false, pointsEarned: 0, totalScore: 0, nextQuestionAtUtc: now, serverTimeUtc: now };
    mockConnection.invoke.mockResolvedValueOnce({
      matchId: 'practice-1', phase: 'reviewing', questionNumber: 1, totalQuestions: 10, totalScore: 0, correctAnswers: 0,
      currentQuestion: null, review, serverTimeUtc: now,
    });

    await client.joinMatch();

    expect(events.at(-1)).toEqual({ name: 'CurrentState', data: expect.objectContaining({
      phase: 'reviewing', currentQuestion: { ...question(), hasAnswered: false, selectedAnswerId: null }, lastQuestionResult: review,
    }) });
  });

  it('al reconectar con la partida terminada muestra el resultado', async () => {
    const { client, events } = await startedClient();
    mockConnection.invoke.mockResolvedValueOnce({
      matchId: 'practice-1', phase: 'finished', questionNumber: 10, totalQuestions: 10, totalScore: 2000, correctAnswers: 9,
      currentQuestion: null, review: null, serverTimeUtc: now,
    });

    await client.joinMatch();

    expect(events.at(-2)).toEqual({ name: 'CurrentState', data: expect.objectContaining({ phase: 'finished', currentQuestion: null }) });
    expect(events.at(-1)).toEqual({ name: 'GameFinished', data: expect.objectContaining({
      players: [expect.objectContaining({ score: 2000, correctAnswers: 9 })],
    }) });
  });

  it('on() devuelve una función que quita al suscriptor', async () => {
    const client = new PracticeTriviaClient('http://api', player);
    const handler = jest.fn();
    client.on('QuestionStarted', handler)();

    server('QuestionStarted', question());

    expect(handler).not.toHaveBeenCalled();
  });
});
