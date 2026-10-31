// Partidas de un solo jugador con las mismas pantallas: modo playground (sin el Shell) y modo práctica (desde el Shell, sin sala).
import { createFixture } from '@aurelia/testing';
import { tasksSettled } from '@aurelia/runtime';
import { GameModule } from '../src/game-module';
import { GAME_FINISHED_EVENT, type GameContext } from '../src/contracts/game-contracts';
import type { Question, TriviaHubEventMap, TriviaHubEventName } from '../src/services/trivia-hub/contract';
import { PracticeTriviaClient } from '../src/services/trivia-hub/practice-trivia-client';
import { SignalRTriviaClient } from '../src/services/trivia-hub/signalr-trivia-client';

interface MockClient {
  handlers: Map<string, (data: never) => void>;
  setState: (state: string) => void;
  connect: jest.Mock<Promise<void>, []>;
  disconnect: jest.Mock<Promise<void>, []>;
  joinMatch: jest.Mock<Promise<void>, unknown[]>;
  submitAnswer: jest.Mock<Promise<void>, unknown[]>;
}
const mockClients: MockClient[] = [];
function mockCreateClient(): MockClient {
  const handlers = new Map<string, (data: never) => void>();
  const client: MockClient & Record<string, unknown> = {
    handlers,
    setState: () => undefined,
    connect: jest.fn(async () => { client.setState('connected'); }),
    disconnect: jest.fn(async () => undefined),
    joinMatch: jest.fn(async () => undefined),
    submitAnswer: jest.fn(async () => undefined),
    on: jest.fn((name: string, handler: (data: never) => void) => { handlers.set(name, handler); return () => handlers.delete(name); }),
    onConnectionStateChanged: jest.fn((handler: (state: string) => void) => {
      client.setState = handler;
      return () => { client.setState = () => undefined; };
    }),
  };
  mockClients.push(client);
  return client;
}
jest.mock('../src/services/trivia-hub/practice-trivia-client', () => ({ PracticeTriviaClient: jest.fn(() => mockCreateClient()) }));
jest.mock('../src/services/trivia-hub/signalr-trivia-client', () => ({ SignalRTriviaClient: jest.fn() }));

const withoutShell: GameContext = { matchId: 'match-dev-001', gameType: 'trivia', currentUser: { id: 'user-dev-1', displayName: 'Jugador 1' }, mode: 'playground' };
const fromRoom: GameContext = { matchId: 'sala', gameType: 'trivia', currentUser: withoutShell.currentUser, participants: [{ userId: 'user-dev-1', displayName: 'Jugador 1' }] };
const practice: GameContext = { matchId: '', gameType: 'trivia', currentUser: withoutShell.currentUser, mode: 'practice' };
const now = (): string => new Date().toISOString();
const question: Question = {
  questionId: 'q1', questionNumber: 1, totalQuestions: 10, text: '¿Qué lenguaje usa Aurelia?', category: 'Tecnología',
  options: [{ answerId: 'a', text: 'TypeScript' }, { answerId: 'b', text: 'COBOL' }],
  expiresAtUtc: new Date(Date.now() + 15000).toISOString(),
};
const latest = (): MockClient => mockClients[mockClients.length - 1];
function failNextClient(configure: (client: MockClient) => void): void {
  jest.mocked(PracticeTriviaClient).mockImplementationOnce(() => {
    const client = mockCreateClient();
    configure(client);
    return client as unknown as PracticeTriviaClient;
  });
}
function emit<E extends TriviaHubEventName>(event: E, data: TriviaHubEventMap[E], client = latest()): void { client.handlers.get(event)?.(data as never); }
const finished = (score: number, correctAnswers: number): TriviaHubEventMap['GameFinished'] => ({
  matchId: 'practice-1', winnerUserId: 'user-dev-1', isTie: false, decidedByTiebreak: false, finishedAtUtc: now(),
  players: [{ userId: 'user-dev-1', displayName: 'Jugador 1', score, correctAnswers, correctAnswersTimeMs: 0, position: 1 }],
});

async function render(context: GameContext = withoutShell) {
  const fixture = await createFixture('<trivia-game-module component.ref="game"></trivia-game-module>', class { public game!: GameModule; }, [GameModule]).started;
  const game = fixture.component.game;
  await game.initialize(context);
  const text = async (): Promise<string> => { await tasksSettled(); return fixture.appHost.textContent ?? ''; };
  const button = async (label: string): Promise<HTMLButtonElement | undefined> => {
    await tasksSettled();
    return Array.from(fixture.appHost.querySelectorAll('button')).find(item => item.textContent?.includes(label));
  };
  return { game, text, button, fixture };
}

beforeEach(() => { jest.clearAllMocks(); mockClients.length = 0; });

describe('modo playground (sin el Shell)', () => {
  it('reemplaza la vista previa por el botón de práctica y juega con las mismas pantallas hasta el resultado', async () => {
    const { game, text, button, fixture } = await render();
    try {
      expect(await text()).toContain('Modo playground');
      expect((await button('Jugar en modo playground'))?.disabled).toBe(false);

      (await button('Jugar en modo playground'))!.click();
      await tasksSettled();
      await new Promise(resolve => setTimeout(resolve, 0));

      expect(PracticeTriviaClient).toHaveBeenCalledWith('http://localhost:5185', withoutShell.currentUser);
      expect(SignalRTriviaClient).not.toHaveBeenCalled();
      expect(latest().joinMatch).toHaveBeenCalledWith({ matchId: 'match-dev-001', userId: 'user-dev-1', participants: [] });
      expect(game.solo).toBe(true);
      expect(game.state).toBe('running');
      expect(fixture.appHost.querySelector('[data-testid="solo-badge"]')?.textContent).toContain('no se guarda en tu historial');
      expect(fixture.appHost.querySelector('[data-testid="solo-offer"]')).toBeNull();

      emit('CurrentState', {
        matchId: 'practice-1', phase: 'playing', serverTimeUtc: now(), totalQuestions: 10, lastQuestionResult: null,
        players: [{ userId: 'user-dev-1', displayName: 'Jugador 1', score: 0, isConnected: true }],
        currentQuestion: { ...question, hasAnswered: false, selectedAnswerId: null },
      });
      expect(await text()).toContain('¿Qué lenguaje usa Aurelia?');
      const option = (await button('TypeScript'))!;
      expect(option.disabled).toBe(false);

      await game.answer('a');
      expect(latest().submitAnswer).toHaveBeenCalledWith({ matchId: 'match-dev-001', questionId: 'q1', answerId: 'a' });
      emit('AnswerReceived', { questionId: 'q1', answerId: 'a', receivedAtUtc: now() });
      await tasksSettled();
      expect(option.disabled).toBe(true);

      emit('QuestionClosed', { questionId: 'q1', correctAnswerId: 'a', selectedAnswerId: 'a', isCorrect: true, pointsEarned: 230, totalScore: 230, nextQuestionAtUtc: now(), serverTimeUtc: now() });
      expect(await text()).toContain('¡Correcto! +230 puntos · Total: 230');

      emit('GameFinished', finished(1800, 8));
      const content = await text();
      expect(content).toContain('Partida terminada');
      expect(content).toContain('1800 puntos · 8 de 10 respuestas correctas');
      expect(content).not.toContain('¡Ganaste!');
      expect(content).not.toContain('Ver mi historial');
      expect(await button('Jugar otra vez')).toBeDefined();
    } finally { await game.dispose(); await fixture.stop(true); }
  });

  it('"Jugar otra vez" cierra la partida anterior y empieza una nueva desde cero', async () => {
    const { game, text, fixture } = await render();
    try {
      await game.playSolo();
      const first = latest();
      emit('GameFinished', finished(900, 4));

      await game.playSolo();

      expect(first.disconnect).toHaveBeenCalledTimes(1);
      expect(mockClients).toHaveLength(2);
      expect(latest().joinMatch).toHaveBeenCalledTimes(1);
      expect(game.result).toBeNull();
      expect(game.phase).toBe('waiting');
      expect(await text()).toContain('Preparando la partida');
      emit('GameFinished', finished(100, 1), first);
      expect(game.result).toBeNull();
    } finally { await game.dispose(); await fixture.stop(true); }
  });

  it('si la API no responde, muestra el error y vuelve a ofrecer jugar', async () => {
    failNextClient(client => client.connect.mockRejectedValueOnce(new Error('No se pudo conectar con Trivia. Revisa que la API esté encendida.')));
    const { game, text, button, fixture } = await render();
    try {
      await game.playSolo();

      const content = await text();
      expect(game.solo).toBe(false);
      expect(content).toContain('No se pudo conectar con Trivia');
      expect(latest().disconnect).toHaveBeenCalled();
      expect((await button('Jugar en modo playground'))?.disabled).toBe(false);

      await game.playSolo();
      expect(game.solo).toBe(true);
      expect(game.error).toBe('');
    } finally { await game.dispose(); await fixture.stop(true); }
  });

  it('usa un mensaje genérico si el error no es un Error', async () => {
    failNextClient(client => client.joinMatch.mockRejectedValueOnce('caído'));
    const game = new GameModule();
    await game.initialize(withoutShell);

    await game.playSolo();

    expect(game.error).toBe('No se pudo iniciar la partida.');
    await game.dispose();
  });

  it('al reconectar recupera la partida', async () => {
    const game = new GameModule();
    await game.initialize(withoutShell);
    await game.playSolo();

    latest().setState('reconnecting');
    latest().setState('connected');
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(latest().joinMatch).toHaveBeenCalledTimes(2);
    expect(game.error).toBe('');

    latest().joinMatch.mockRejectedValueOnce(new Error('PRACTICE_NOT_FOUND'));
    latest().setState('connected');
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(game.error).toContain('No se pudo recuperar la partida');
    await game.dispose();
    expect(mockClients[0].disconnect).toHaveBeenCalled();
  });

  it('pausado indica que la partida sigue en el servidor y no deja empezar otra', async () => {
    const { game, text, fixture } = await render();
    try {
      await game.playSolo();
      await game.pause();

      expect(await text()).toContain('La partida continúa en el servidor.');
      await game.playSolo();
      expect(mockClients).toHaveLength(1);
    } finally { await game.dispose(); await fixture.stop(true); }
  });

  it('no se ofrece en una partida de una sala', async () => {
    const { game, text, fixture } = await render(fromRoom);
    try {
      await game.playSolo();

      expect(PracticeTriviaClient).not.toHaveBeenCalled();
      expect(await text()).not.toContain('Jugar en modo');
      expect(game.solo).toBe(false);
    } finally { await game.dispose(); await fixture.stop(true); }
  });

  it('después de dispose() no empieza ninguna partida', async () => {
    const game = new GameModule();
    await game.initialize(withoutShell);
    await game.dispose();

    await game.playSolo();

    expect(PracticeTriviaClient).not.toHaveBeenCalled();
  });

  it('no avisa al Shell cuando termina: no hay sala que cerrar', async () => {
    const finishedEvent = jest.fn();
    window.addEventListener(GAME_FINISHED_EVENT, finishedEvent);
    const game = new GameModule();
    await game.initialize(withoutShell);
    await game.playSolo();

    emit('GameFinished', finished(500, 3));

    expect(finishedEvent).not.toHaveBeenCalled();
    window.removeEventListener(GAME_FINISHED_EVENT, finishedEvent);
    await game.dispose();
  });
});

describe('modo práctica (desde el Shell, sin sala)', () => {
  it('start() empieza solo, sin matchId ni jugadores, y lo indica en pantalla', async () => {
    const { game, fixture } = await render(practice);
    try {
      await game.start();
      await tasksSettled();

      expect(PracticeTriviaClient).toHaveBeenCalledWith('http://localhost:5185', practice.currentUser);
      expect(SignalRTriviaClient).not.toHaveBeenCalled();
      expect(latest().joinMatch).toHaveBeenCalledTimes(1);
      expect(game.solo).toBe(true);
      expect(game.state).toBe('running');
      expect(fixture.appHost.querySelector('[data-testid="solo-badge"]')?.textContent).toContain('Modo práctica');
      expect(fixture.appHost.querySelector('[data-testid="solo-offer"]')).toBeNull();
    } finally { await game.dispose(); await fixture.stop(true); }
  });

  it('si la API no responde, muestra el error y permite reintentar', async () => {
    failNextClient(client => client.connect.mockRejectedValueOnce(new Error('No se pudo conectar con Trivia.')));
    const { game, text, button, fixture } = await render(practice);
    try {
      await game.start();

      expect(await text()).toContain('No se pudo conectar con Trivia.');
      (await button('Jugar en modo práctica'))!.click();
      await tasksSettled();
      await new Promise(resolve => setTimeout(resolve, 0));
      expect(game.solo).toBe(true);
      expect(game.error).toBe('');
    } finally { await game.dispose(); await fixture.stop(true); }
  });

  it('al reanudar después de pausar no empieza otra partida', async () => {
    const game = new GameModule();
    await game.initialize(practice);
    await game.start();
    await game.pause();

    await game.start();

    expect(game.state).toBe('running');
    expect(mockClients).toHaveLength(1);
    await game.dispose();
  });
});
