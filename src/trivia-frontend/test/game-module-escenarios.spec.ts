// Casos de GameModule fuera del camino feliz: errores, reconexión, pausa, historial y resultado final.
import { createFixture } from '@aurelia/testing';
import { tasksSettled } from '@aurelia/runtime';
import { GameModule } from '../src/game-module';
import type { GameContext } from '../src/contracts/game-contracts';
import type { Player, Question, TriviaHubEventMap, TriviaHubEventName } from '../src/services/trivia-hub/contract';
import { SignalRTriviaClient } from '../src/services/trivia-hub/signalr-trivia-client';

const mockHandlers = new Map<string, (data: never) => void>();
let mockStateHandler: (state: string) => void = () => undefined;
const mockClient = {
  connect: jest.fn(async () => { mockStateHandler('connected'); }),
  disconnect: jest.fn(async () => undefined),
  joinMatch: jest.fn(async () => undefined),
  submitAnswer: jest.fn(async () => undefined),
  on: jest.fn((name: string, handler: (data: never) => void) => { mockHandlers.set(name, handler); return () => mockHandlers.delete(name); }),
  onConnectionStateChanged: jest.fn((handler: (state: string) => void) => { mockStateHandler = handler; return () => undefined; }),
};
jest.mock('../src/services/trivia-hub/signalr-trivia-client', () => ({ SignalRTriviaClient: jest.fn(() => mockClient) }));

const preview: GameContext = { matchId: 'match', gameType: 'trivia', currentUser: { id: 'one', displayName: 'Uno' } };
const ctx: GameContext = { ...preview, getAccessToken: jest.fn(async () => 'trivia-token'), getMatchmakingAccessToken: jest.fn(async () => 'room-token') };
const players: Player[] = [
  { userId: 'one', displayName: 'Uno', score: 0, isConnected: true },
  { userId: 'two', displayName: 'Dos', score: 0, isConnected: false },
];
const now = (): string => new Date().toISOString();
const question = (expiresInMs = 15000): Question => ({
  questionId: 'q1', questionNumber: 1, totalQuestions: 10, text: '¿Capital de Costa Rica?', category: 'Geografía',
  options: [{ answerId: 'a', text: 'San José' }, { answerId: 'b', text: 'Cartago' }],
  expiresAtUtc: new Date(Date.now() + expiresInMs).toISOString(),
});
function emit<E extends TriviaHubEventName>(event: E, data: TriviaHubEventMap[E]): void { mockHandlers.get(event)?.(data as never); }
const flush = (): Promise<void> => new Promise(resolve => setTimeout(resolve, 0));
async function running(): Promise<GameModule> {
  const game = new GameModule();
  await game.initialize(ctx);
  await game.start();
  return game;
}
async function renderRunning() {
  const fixture = await createFixture('<trivia-game-module component.ref="game"></trivia-game-module>', class { public game!: GameModule; }, [GameModule]).started;
  const game = fixture.component.game;
  await game.initialize(ctx);
  await game.start();
  return { game, text: async () => { await tasksSettled(); return fixture.appHost.textContent ?? ''; }, host: fixture.appHost };
}

let previousFetch: typeof fetch;
beforeEach(() => { jest.clearAllMocks(); mockHandlers.clear(); previousFetch = globalThis.fetch; });
afterEach(() => { globalThis.fetch = previousFetch; });

describe('errores', () => {
  it('si no puede conectar, start() falla, limpia la conexión y permite reintentar', async () => {
    mockClient.connect.mockRejectedValueOnce(new Error('No se pudo conectar con Trivia.'));
    const game = new GameModule();
    await game.initialize(ctx);

    await expect(game.start()).rejects.toThrow('No se pudo conectar');
    expect(game.state).toBe('initialized');
    expect(mockClient.disconnect).toHaveBeenCalledTimes(1);
    expect(mockHandlers.size).toBe(0);

    await game.start();
    expect(game.state).toBe('running');
    expect(SignalRTriviaClient).toHaveBeenCalledTimes(2);
    await game.dispose();
  });

  it('si Matchmaking rechaza la entrada, start() propaga el error sin quedar en running', async () => {
    mockClient.joinMatch.mockRejectedValueOnce(new Error('PLAYER_NOT_IN_MATCH'));
    const game = new GameModule();
    await game.initialize(ctx);

    await expect(game.start()).rejects.toThrow('PLAYER_NOT_IN_MATCH');
    expect(game.state).toBe('initialized');
    expect(mockClient.disconnect).toHaveBeenCalledTimes(1);
  });

  it('si el Shell no entrega el token de Matchmaking, start() falla sin entrar a la partida', async () => {
    (ctx.getMatchmakingAccessToken as jest.Mock).mockRejectedValueOnce(new Error('LOGIN_REQUIRED'));
    const game = new GameModule();
    await game.initialize(ctx);

    await expect(game.start()).rejects.toThrow('LOGIN_REQUIRED');
    expect(mockClient.joinMatch).not.toHaveBeenCalled();
    expect(game.state).toBe('initialized');
  });

  it('rechaza initialize() dos veces', async () => {
    const game = new GameModule();
    await game.initialize(ctx);
    await expect(game.initialize(ctx)).rejects.toThrow('ya fue inicializado');
  });

  it('muestra el mensaje de GameError enviado por el servidor', async () => {
    const game = await running();
    emit('GameError', { code: 'QUESTION_CLOSED', message: 'La pregunta ya cerró.' });
    expect(game.error).toBe('La pregunta ya cerró.');
    await game.dispose();
  });

  it('si la respuesta no se confirma muestra un error y deja volver a intentar', async () => {
    const game = await running();
    emit('QuestionStarted', { ...question(), serverTimeUtc: now() });
    mockClient.submitAnswer.mockRejectedValueOnce(new Error('timeout'));

    await game.answer('a');
    expect(game.error).toContain('no fue confirmada');
    expect(game.submitting).toBe(false);

    await game.answer('a');
    expect(mockClient.submitAnswer).toHaveBeenCalledTimes(2);
    expect(game.error).toBe('');
    await game.dispose();
  });

  it('no envía respuestas sin conexión ni con el tiempo vencido', async () => {
    const game = await running();
    emit('QuestionStarted', { ...question(), serverTimeUtc: now() });
    mockStateHandler('reconnecting');
    await game.answer('a');

    mockStateHandler('connected');
    emit('QuestionStarted', { ...question(-1000), serverTimeUtc: now() });
    await game.answer('a');

    expect(game.seconds).toBeLessThanOrEqual(0);
    expect(mockClient.submitAnswer).not.toHaveBeenCalled();
    await game.dispose();
  });

  it('no abre una segunda conexión si start() se llama mientras conecta', async () => {
    let finishConnect: () => void = () => undefined;
    mockClient.connect.mockImplementationOnce(() => new Promise<void>(resolve => { finishConnect = () => { mockStateHandler('connected'); resolve(); }; }));
    const game = new GameModule();
    await game.initialize(ctx);

    const first = game.start();
    await game.start();
    finishConnect();
    await first;

    expect(SignalRTriviaClient).toHaveBeenCalledTimes(1);
    expect(game.state).toBe('running');
    await game.dispose();
  });

  it('si dispose() llega mientras conecta, cierra la conexión y no entra a la partida', async () => {
    let finishConnect: () => void = () => undefined;
    mockClient.connect.mockImplementationOnce(() => new Promise<void>(resolve => { finishConnect = resolve; }));
    const game = new GameModule();
    await game.initialize(ctx);

    const pending = game.start();
    await game.dispose();
    finishConnect();
    await pending;

    expect(game.state).toBe('disposed');
    expect(mockClient.joinMatch).not.toHaveBeenCalled();
    expect(mockClient.disconnect).toHaveBeenCalledTimes(2);
  });
});

describe('reconexión', () => {
  it('el snapshot CurrentState restaura la pregunta activa y bloquea la respuesta ya enviada', async () => {
    const game = await running();
    emit('CurrentState', {
      matchId: 'match', phase: 'playing', serverTimeUtc: now(), totalQuestions: 10, players,
      currentQuestion: { ...question(), hasAnswered: true, selectedAnswerId: 'b' }, lastQuestionResult: null,
    });

    expect(game.phase).toBe('playing');
    expect(game.question?.questionId).toBe('q1');
    expect(game.answered).toBe(true);
    expect(game.selectedAnswerId).toBe('b');
    expect(game.seconds).toBeGreaterThan(0);
    expect(game.players).toHaveLength(2);

    await game.answer('a');
    expect(mockClient.submitAnswer).not.toHaveBeenCalled();
    await game.dispose();
  });

  it('el snapshot en revisión muestra el resultado de la última pregunta', async () => {
    const game = await running();
    emit('CurrentState', {
      matchId: 'match', phase: 'reviewing', serverTimeUtc: now(), totalQuestions: 10, players, currentQuestion: null,
      lastQuestionResult: { questionId: 'q1', correctAnswerId: 'a', selectedAnswerId: 'a', isCorrect: true, pointsEarned: 200, totalScore: 200, nextQuestionAtUtc: now(), serverTimeUtc: now() },
    });

    expect(game.phase).toBe('reviewing');
    expect(game.review?.totalScore).toBe(200);
    expect(game.selectedAnswerId).toBe('a');
    expect(game.answered).toBe(false);
    await game.dispose();
  });

  it('actualiza quién está conectado y el marcador', async () => {
    const game = await running();
    emit('CurrentState', { matchId: 'match', phase: 'waiting', serverTimeUtc: now(), totalQuestions: 10, players, currentQuestion: null, lastQuestionResult: null });

    emit('PlayerJoined', { userId: 'two', displayName: 'Dos' });
    expect(game.players.find(p => p.userId === 'two')?.isConnected).toBe(true);
    emit('PlayerDisconnected', { userId: 'one', displayName: 'Uno' });
    expect(game.players.find(p => p.userId === 'one')?.isConnected).toBe(false);
    emit('PlayerReconnected', { userId: 'one', displayName: 'Uno' });
    expect(game.players.find(p => p.userId === 'one')?.isConnected).toBe(true);

    emit('ScoreboardUpdated', { players: [{ ...players[1], score: 240 }, { ...players[0], score: 100 }] });
    expect(game.players.map(p => p.score)).toEqual([240, 100]);
    await game.dispose();
  });

  it('ignora AnswerReceived de otra pregunta', async () => {
    const game = await running();
    emit('QuestionStarted', { ...question(), serverTimeUtc: now() });
    emit('AnswerReceived', { questionId: 'otra', answerId: 'a', receivedAtUtc: now() });
    expect(game.answered).toBe(false);
    await game.dispose();
  });

  it('si falla el reingreso después de reconectar muestra cómo recuperar la partida', async () => {
    const game = await running();
    mockClient.joinMatch.mockRejectedValueOnce(new Error('MATCH_NOT_FOUND'));

    mockStateHandler('reconnecting');
    expect(game.connection).toBe('reconnecting');
    mockStateHandler('connected');
    await flush();

    expect(game.error).toContain('No se pudo recuperar la partida');
    await game.dispose();
  });

  it('después de dispose() no vuelve a entrar si la conexión se recupera', async () => {
    const game = await running();
    await game.dispose();
    mockStateHandler('connected');
    await flush();
    expect(mockClient.joinMatch).toHaveBeenCalledTimes(1);
  });

  it('muestra el aviso de conexión mientras reconecta', async () => {
    const { game, text } = await renderRunning();
    mockStateHandler('reconnecting');
    expect(await text()).toContain('Conexión: reconnecting');
    mockStateHandler('connected');
    expect(await text()).not.toContain('Conexión:');
    await game.dispose();
  });
});

describe('pausa', () => {
  it('en pausa muestra el aviso, desactiva las opciones y no envía respuestas', async () => {
    const { game, text, host } = await renderRunning();
    emit('QuestionStarted', { ...question(), serverTimeUtc: now() });
    await game.pause();

    expect(await text()).toContain('La vista está pausada');
    const buttons = [...host.querySelectorAll<HTMLButtonElement>('.trivia-game-options button')];
    expect(buttons).toHaveLength(2);
    expect(buttons.every(button => button.disabled)).toBe(true);
    await game.answer('a');
    expect(mockClient.submitAnswer).not.toHaveBeenCalled();
    await game.dispose();
  });

  it('start() reanuda sin abrir otra conexión y permite responder', async () => {
    const game = await running();
    emit('QuestionStarted', { ...question(), serverTimeUtc: now() });
    await game.pause();
    await game.start();

    expect(game.state).toBe('running');
    expect(SignalRTriviaClient).toHaveBeenCalledTimes(1);
    await game.answer('a');
    expect(mockClient.submitAnswer).toHaveBeenCalledWith({ matchId: 'match', questionId: 'q1', answerId: 'a' });
    await game.dispose();
  });

  it('pause() después de dispose() no cambia el estado', async () => {
    const game = await running();
    await game.dispose();
    await game.pause();
    expect(game.state).toBe('disposed');
  });
});

describe('historial', () => {
  it('si la API responde con error muestra el mensaje y no guarda datos', async () => {
    globalThis.fetch = jest.fn().mockResolvedValue({ ok: false, json: async () => ({}) });
    const game = await running();

    await game.loadHistory();

    expect(game.error).toContain('No se pudo cargar tu historial');
    expect(game.history).toEqual([]);
    expect(game.statistics).toBeNull();
    expect(game.loadingHistory).toBe(false);
    await game.dispose();
  });

  it('si la red falla muestra el mensaje', async () => {
    globalThis.fetch = jest.fn().mockRejectedValue(new TypeError('Failed to fetch'));
    const game = await running();
    await game.loadHistory();
    expect(game.error).toContain('No se pudo cargar tu historial');
    await game.dispose();
  });

  it('no consulta la API sin initialize() ni mientras ya está cargando', async () => {
    const pending: ((value: unknown) => void)[] = [];
    const fetchMock = jest.fn(() => new Promise(resolve => { pending.push(resolve); }));
    globalThis.fetch = fetchMock as unknown as typeof fetch;
    const finish = (value: unknown): void => { pending.forEach(resolve => resolve(value)); };

    await new GameModule().loadHistory();
    expect(fetchMock).not.toHaveBeenCalled();

    const game = await running();
    const first = game.loadHistory();
    await game.loadHistory();
    await flush();
    expect(fetchMock).toHaveBeenCalledTimes(2);
    finish({ ok: true, json: async () => [] });
    await game.dispose();
    await first;
    expect(game.history).toEqual([]);
  });

  it('pide el historial con el token de Trivia', async () => {
    const fetchMock = jest.fn().mockResolvedValue({ ok: true, json: async () => [] });
    globalThis.fetch = fetchMock;
    const game = await running();

    await game.loadHistory();

    expect(fetchMock.mock.calls[1][0]).toBe('http://localhost:5185/api/games/trivia/players/one/stats');
    expect(fetchMock.mock.calls[1][1].headers.Authorization).toBe('Bearer trivia-token');
    await game.dispose();
  });

  it('myScore() devuelve 0 si el jugador no aparece en la partida', async () => {
    const game = await running();
    expect(game.myScore({ matchId: 'm', players: [{ userId: 'two', score: 500, position: 1 }] })).toBe(0);
    await game.dispose();
  });
});

describe('vista previa sin tokens', () => {
  it('start() queda en running sin conectarse al hub y muestra el aviso', async () => {
    const fixture = await createFixture('<trivia-game-module component.ref="game"></trivia-game-module>', class { public game!: GameModule; }, [GameModule]).started;
    const game = fixture.component.game;
    await game.initialize(preview);
    await game.start();
    await tasksSettled();

    expect(game.preview).toBe(true);
    expect(game.state).toBe('running');
    expect(SignalRTriviaClient).not.toHaveBeenCalled();
    expect(fixture.appHost.textContent).toContain('Vista previa');
    expect(fixture.appHost.textContent).not.toContain('Conexión:');
    await game.dispose();
  });

  it('no consulta el historial sin token', async () => {
    const fetchMock = jest.fn();
    globalThis.fetch = fetchMock;
    const game = new GameModule();
    await game.initialize(preview);

    await game.loadHistory();

    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe('resultado final', () => {
  const finished = (overrides: Partial<TriviaHubEventMap['GameFinished']>): TriviaHubEventMap['GameFinished'] => ({
    matchId: 'match', winnerUserId: null, isTie: false, decidedByTiebreak: false, finishedAtUtc: now(),
    players: [
      { userId: 'two', displayName: 'Dos', score: 1500, correctAnswers: 7, correctAnswersTimeMs: 21000, position: 1 },
      { userId: 'one', displayName: 'Uno', score: 1500, correctAnswers: 7, correctAnswersTimeMs: 25000, position: 2 },
    ],
    ...overrides,
  });

  it('muestra que perdió por desempate y la tabla de posiciones', async () => {
    const { game, text } = await renderRunning();
    emit('GameFinished', finished({ winnerUserId: 'two', decidedByTiebreak: true }));

    const content = await text();
    expect(game.phase).toBe('finished');
    expect(content).toContain('Partida finalizada');
    expect(content).toContain('El tiempo de las respuestas correctas decidió el desempate.');
    expect(content).toContain('1. Dos: 1500 puntos · 7 aciertos');
    expect(content).toContain('2. Uno: 1500 puntos · 7 aciertos');
    await game.dispose();
  });

  it('muestra el empate cuando no hay ganador', async () => {
    const { game, text } = await renderRunning();
    emit('GameFinished', finished({ isTie: true, players: finished({}).players.map(p => ({ ...p, position: 1 })) }));

    const content = await text();
    expect(content).toContain('¡Empate!');
    expect(content).not.toContain('desempate');
    await game.dispose();
  });

  it('el botón del resultado carga y muestra el historial', async () => {
    globalThis.fetch = jest.fn()
      .mockResolvedValueOnce({ ok: true, json: async () => [{ matchId: 'match', players: [{ userId: 'one', score: 1500, position: 2 }] }] })
      .mockResolvedValueOnce({ ok: true, json: async () => ({ matchesPlayed: 3, wins: 1, averageScore: 1200, bestScore: 1800 }) });
    const { game, text, host } = await renderRunning();
    emit('GameFinished', finished({ winnerUserId: 'two' }));
    await text();

    const button = [...host.querySelectorAll<HTMLButtonElement>('button')].find(b => b.textContent?.includes('historial'));
    button?.click();
    await flush();

    const content = await text();
    expect(content).toContain('Mi historial');
    expect(content).toContain('Partidas: 3 · Victorias: 1 · Promedio: 1200 · Mejor puntaje: 1800');
    expect(content).toContain('match · 1500 puntos');
    await game.dispose();
  });
});
