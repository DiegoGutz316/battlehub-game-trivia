import { GameModule } from '../../src/trivia-frontend/src/game-module';
import type { GameContext } from '../../src/trivia-frontend/src/contracts/game-contracts';
import { createFixture } from '@aurelia/testing';
import { tasksSettled } from '@aurelia/runtime';
import type { TriviaHubEventMap, TriviaHubEventName } from '../../src/trivia-frontend/src/services/trivia-hub/contract';

const mockHandlers = new Map<string, (data: never) => void>();
let mockStateHandler: (state: string) => void;
const mockClient = {
  connect: jest.fn(async () => { mockStateHandler('connected'); }),
  disconnect: jest.fn(async () => undefined),
  joinMatch: jest.fn(async () => undefined),
  submitAnswer: jest.fn(async () => undefined),
  on: jest.fn((name: string, handler: (data: never) => void) => { mockHandlers.set(name, handler); return () => mockHandlers.delete(name); }),
  onConnectionStateChanged: jest.fn((handler: (state: string) => void) => { mockStateHandler = handler; return () => undefined; }),
};
jest.mock('../../src/trivia-frontend/src/services/trivia-hub/signalr-trivia-client', () => ({ SignalRTriviaClient: jest.fn(() => mockClient) }));
const ctx: GameContext = { matchId: 'match', gameType: 'trivia', currentUser: { id: 'auth0|one', displayName: 'Uno' },
  getAccessToken: jest.fn(async () => 'trivia-token'), getMatchmakingAccessToken: jest.fn(async () => 'room-token') };
function emit<E extends TriviaHubEventName>(event: E, data: TriviaHubEventMap[E]): void { mockHandlers.get(event)?.(data as never); }

beforeEach(() => { jest.clearAllMocks(); mockHandlers.clear(); });
afterEach(() => { jest.useRealTimers(); });

it('renders the question, answer lock and final scoreboard in Aurelia', async () => {
  const fixture = await createFixture('<trivia-game-module component.ref="game"></trivia-game-module>', class { public game!: GameModule; }, [GameModule]).started;
  const game = fixture.component.game;
  try {
    await game.initialize(ctx); await game.start();
    const now = new Date().toISOString();
    emit('QuestionStarted', { questionId: 'q1', questionNumber: 1, totalQuestions: 10, text: '¿Qué lenguaje usamos?', category: 'Tecnología',
      options: [{ answerId: 'a', text: 'TypeScript' }, { answerId: 'b', text: 'Java' }], serverTimeUtc: now, expiresAtUtc: new Date(Date.now() + 15000).toISOString() });
    await tasksSettled();
    expect(fixture.appHost.textContent).toContain('¿Qué lenguaje usamos?');
    const button = fixture.appHost.querySelector('.trivia-game-options button') as HTMLButtonElement;
    expect(button.disabled).toBe(false);
    emit('AnswerReceived', { questionId: 'q1', answerId: 'a', receivedAtUtc: now }); await tasksSettled();
    expect(button.disabled).toBe(true);
    emit('GameFinished', { matchId: 'match', players: [{ userId: 'auth0|one', displayName: 'Uno', score: 2200, correctAnswers: 10, correctAnswersTimeMs: 30000, position: 1 }], winnerUserId: 'auth0|one', isTie: false, decidedByTiebreak: false, finishedAtUtc: now });
    await tasksSettled();
    expect(fixture.appHost.textContent).toContain('¡Ganaste!');
    expect(fixture.appHost.textContent).toContain('2200 puntos');
  } finally { await game.dispose(); await fixture.stop(true); }
});
it('fetches current user history and statistics with a Trivia token', async () => {
  const game = new GameModule(); await game.initialize(ctx);
  const fetchMock = jest.fn().mockResolvedValueOnce({ ok: true, json: async () => [{ matchId: 'match', players: [{ userId: 'auth0|one', score: 2200, position: 1 }] }] })
    .mockResolvedValueOnce({ ok: true, json: async () => ({ matchesPlayed: 1, wins: 1, averageScore: 2200, bestScore: 2200 }) });
  const previous = globalThis.fetch; globalThis.fetch = fetchMock;
  try {
    await game.loadHistory();
    expect(fetchMock.mock.calls[0][0]).toBe('http://localhost:5185/api/games/trivia/players/auth0%7Cone/history');
    expect(fetchMock.mock.calls[0][1].headers.Authorization).toBe('Bearer trivia-token');
    expect(game.statistics?.wins).toBe(1); expect(game.myScore(game.history[0])).toBe(2200);
  } finally { globalThis.fetch = previous; await game.dispose(); }
});

it('joins with the room token, locks confirmed answers and shows server result', async () => {
  const game = new GameModule(); await game.initialize(ctx); await game.start();
  expect(mockClient.joinMatch).toHaveBeenCalledWith({ matchId: 'match', matchmakingAccessToken: 'room-token' });
  const now = new Date().toISOString();
  emit('QuestionStarted', { questionId: 'q1', questionNumber: 1, totalQuestions: 10, text: 'Pregunta', category: 'General', options: [{ answerId: 'a', text: 'A' }], serverTimeUtc: now, expiresAtUtc: new Date(Date.now() + 15000).toISOString() });
  await game.answer('a'); expect(mockClient.submitAnswer).toHaveBeenCalledTimes(1);
  emit('AnswerReceived', { questionId: 'q1', answerId: 'a', receivedAtUtc: now });
  await game.answer('a'); expect(mockClient.submitAnswer).toHaveBeenCalledTimes(1);
  emit('QuestionClosed', { questionId: 'q1', correctAnswerId: 'a', selectedAnswerId: 'a', isCorrect: true, pointsEarned: 220, totalScore: 220, nextQuestionAtUtc: now, serverTimeUtc: now });
  expect(game.review?.pointsEarned).toBe(220); expect(game.phase).toBe('reviewing');
  emit('GameFinished', { matchId: 'match', players: [], winnerUserId: 'auth0|one', isTie: false, decidedByTiebreak: false, finishedAtUtc: now });
  expect(game.phase).toBe('finished'); await game.dispose(); expect(mockClient.disconnect).toHaveBeenCalled();
});
it('gets a fresh room token and snapshot after reconnecting', async () => {
  const game = new GameModule(); await game.initialize(ctx); await game.start();
  mockStateHandler('reconnecting'); mockStateHandler('connected');
  await Promise.resolve(); await Promise.resolve();
  expect(ctx.getMatchmakingAccessToken).toHaveBeenCalledTimes(2);
  expect(mockClient.joinMatch).toHaveBeenCalledTimes(2);
  await game.dispose();
});
it('refuses partially configured authorization', async () => {
  await expect(new GameModule().initialize({ ...ctx, getMatchmakingAccessToken: undefined })).rejects.toThrow('autorización');
});
it('unsubscribes and clears the countdown after disposal', async () => {
  jest.useFakeTimers();
  const game = new GameModule(); await game.initialize(ctx); await game.start();
  await game.dispose(); expect(mockHandlers.size).toBe(0); expect(jest.getTimerCount()).toBe(0); expect(game.context).toBeNull();
});
