import { customElement } from 'aurelia';
import template from './game-module.html';
import './game-module.css';
import type { GameContext, GameModule as IGameModule } from './contracts/game-contracts';
import type { TriviaHubClient, Unsubscribe } from './services/trivia-hub/trivia-hub-client';
import { SignalRTriviaClient } from './services/trivia-hub/signalr-trivia-client';
import { ServerClock } from './services/trivia-hub/server-clock';
import type { GamePhase, Player, Question, QuestionClosedEvent, GameFinishedEvent } from './services/trivia-hub/contract';

export type GameState = 'created' | 'initialized' | 'running' | 'paused' | 'disposed';
interface HistoryItem { matchId: string; players: { userId: string; score: number; position: number }[] }
interface Statistics { matchesPlayed: number; wins: number; averageScore: number; bestScore: number }
const apiUrl = (process.env.TRIVIA_API_URL || 'http://localhost:5185').replace(/\/$/, '');

@customElement({ name: 'trivia-game-module', template })
export class GameModule implements IGameModule {
  public context: GameContext | null = null;
  public state: GameState = 'created';
  public phase: GamePhase = 'waiting';
  public players: Player[] = [];
  public question: Question | null = null;
  public review: QuestionClosedEvent | null = null;
  public result: GameFinishedEvent | null = null;
  public selectedAnswerId: string | null = null;
  public answered = false;
  public submitting = false;
  public seconds = 0;
  public connection = 'disconnected';
  public error = '';
  public preview = false;
  public loadingHistory = false;
  public history: HistoryItem[] = [];
  public statistics: Statistics | null = null;
  private client?: TriviaHubClient;
  private readonly clock = new ServerClock();
  private subscriptions: Unsubscribe[] = [];
  private timer?: ReturnType<typeof setInterval>;
  private connecting = false;
  private joined = false;
  private readonly abort = new AbortController();

  public async initialize(context: GameContext): Promise<void> {
    if (this.state === 'disposed') throw new Error('El juego ya fue liberado con dispose()');
    if (!context?.matchId || context.gameType !== 'trivia' || !context.currentUser?.id)
      throw new Error('Contexto inválido: falta matchId, gameType trivia o currentUser');
    if (this.state !== 'created') throw new Error('El juego ya fue inicializado');
    if (Boolean(context.getAccessToken) !== Boolean(context.getMatchmakingAccessToken))
      throw new Error('El Shell debe entregar autorización de Trivia y de Matchmaking.');
    this.context = context;
    this.preview = !context.getAccessToken;
    this.state = 'initialized';
  }
  private live(): boolean { return this.state !== 'disposed'; }
  private async join(): Promise<void> {
    const context = this.context;
    if (!context || !this.live()) return;
    const token = await context.getMatchmakingAccessToken!();
    if (!this.live()) return;
    await this.client!.joinMatch({ matchId: context.matchId, matchmakingAccessToken: token });
    this.joined = true;
  }
  private subscribe(client: TriviaHubClient): void {
    this.subscriptions = [
      client.on('CurrentState', data => {
        this.clock.sync(data.serverTimeUtc); this.phase = data.phase; this.players = data.players;
        this.question = data.currentQuestion; this.review = data.lastQuestionResult;
        this.answered = data.currentQuestion?.hasAnswered ?? false;
        this.selectedAnswerId = data.currentQuestion?.selectedAnswerId ?? data.lastQuestionResult?.selectedAnswerId ?? null;
        this.tick();
      }),
      client.on('QuestionStarted', data => {
        this.clock.sync(data.serverTimeUtc); this.question = data; this.phase = 'playing';
        this.review = null; this.answered = false; this.submitting = false; this.selectedAnswerId = null; this.error = ''; this.tick();
      }),
      client.on('AnswerReceived', data => {
        if (this.question?.questionId === data.questionId) { this.answered = true; this.selectedAnswerId = data.answerId; }
      }),
      client.on('QuestionClosed', data => {
        this.clock.sync(data.serverTimeUtc); this.phase = 'reviewing'; this.review = data;
        this.selectedAnswerId = data.selectedAnswerId; this.submitting = false;
      }),
      client.on('ScoreboardUpdated', data => { this.players = data.players; }),
      client.on('PlayerJoined', data => { this.markConnected(data.userId, true); }),
      client.on('PlayerReconnected', data => { this.markConnected(data.userId, true); }),
      client.on('PlayerDisconnected', data => { this.markConnected(data.userId, false); }),
      client.on('GameFinished', data => { this.phase = 'finished'; this.result = data; }),
      client.on('GameError', data => { this.error = data.message; }),
      client.onConnectionStateChanged(state => {
        this.connection = state;
        if (state === 'connected' && this.joined && this.live()) void this.join().catch(() => {
          if (this.live()) this.error = 'No se pudo recuperar la partida. Reabre el juego desde Salas.';
        });
      }),
    ];
  }
  private markConnected(id: string, connected: boolean): void {
    this.players = this.players.map(player => player.userId === id ? { ...player, isConnected: connected } : player);
  }
  private tick(): void {
    if (this.question && this.phase === 'playing') this.seconds = Math.ceil(this.clock.countdown(this.question.expiresAtUtc).remainingMs / 1000);
  }
  public async start(): Promise<void> {
    if (!this.live()) throw new Error('El juego ya fue liberado con dispose()');
    if (!this.context) throw new Error('Falta llamar a initialize()');
    if (this.connecting) return;
    if (this.preview) { this.state = 'running'; return; }
    if (this.client) { this.state = 'running'; return; }
    this.connecting = true;
    const client = new SignalRTriviaClient(apiUrl, this.context.getAccessToken!);
    this.client = client;
    this.subscribe(client);
    try {
      await client.connect();
      if (!this.live()) { await client.disconnect(); return; }
      await this.join();
      if (!this.live()) return;
      this.state = 'running';
      this.timer = setInterval(() => this.tick(), 100);
    } catch (error) {
      await client.disconnect(); this.subscriptions.forEach(off => off()); this.subscriptions = []; this.client = undefined;
      throw error;
    } finally { this.connecting = false; }
  }
  public async answer(answerId: string): Promise<void> {
    if (this.state !== 'running' || this.phase !== 'playing' || this.connection !== 'connected' || this.answered || this.submitting || this.seconds <= 0 || !this.question || !this.context) return;
    this.submitting = true; this.error = '';
    try { await this.client!.submitAnswer({ matchId: this.context.matchId, questionId: this.question.questionId, answerId }); }
    catch { if (this.live()) this.error = 'La respuesta no fue confirmada. Revisa el tiempo y tu conexión.'; }
    finally { this.submitting = false; }
  }
  public async loadHistory(): Promise<void> {
    const context = this.context;
    if (!context?.getAccessToken || this.loadingHistory || !this.live()) return;
    this.loadingHistory = true; this.error = '';
    try {
      const token = await context.getAccessToken();
      if (!this.live()) return;
      const base = `${apiUrl}/api/games/trivia/players/${encodeURIComponent(context.currentUser.id)}`;
      const responses = await Promise.all(['history', 'stats'].map(path => fetch(`${base}/${path}`, {
        headers: { Authorization: `Bearer ${token}` }, signal: this.abort.signal,
      })));
      if (responses.some(response => !response.ok)) throw new Error('No se pudo cargar tu historial.');
      const [history, statistics] = await Promise.all(responses.map(response => response.json()));
      if (this.live()) { this.history = history; this.statistics = statistics; }
    } catch { if (this.live()) this.error = 'No se pudo cargar tu historial. Revisa la API y reintenta.'; }
    finally { this.loadingHistory = false; }
  }
  public async pause(): Promise<void> { if (this.state === 'running') this.state = 'paused'; }
  public myScore(item: HistoryItem): number { return item.players.find(player => player.userId === this.context?.currentUser.id)?.score ?? 0; }
  public async dispose(): Promise<void> {
    this.state = 'disposed'; this.abort.abort(); clearInterval(this.timer);
    this.subscriptions.forEach(off => off()); this.subscriptions = [];
    await this.client?.disconnect(); this.client = undefined; this.context = null;
  }
}
