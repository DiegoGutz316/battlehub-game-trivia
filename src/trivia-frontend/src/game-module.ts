import { customElement } from 'aurelia';
import template from './game-module.html';
import './game-module.css';
import { GAME_FINISHED_EVENT, type GameContext, type GameFinishedDetail, type GameMode, type GameModule as IGameModule } from './contracts/game-contracts';
import type { TriviaHubClient, Unsubscribe } from './services/trivia-hub/trivia-hub-client';
import { SignalRTriviaClient } from './services/trivia-hub/signalr-trivia-client';
import { PracticeTriviaClient } from './services/trivia-hub/practice-trivia-client';
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
  public mode: GameMode = 'match';
  /** Partida de un solo jugador en curso (práctica o playground): no se guarda en el historial. */
  public solo = false;
  public connecting = false;
  public loadingHistory = false;
  public history: HistoryItem[] = [];
  public statistics: Statistics | null = null;
  private client?: TriviaHubClient;
  private readonly clock = new ServerClock();
  private subscriptions: Unsubscribe[] = [];
  private timer?: ReturnType<typeof setInterval>;
  private joined = false;
  private readonly abort = new AbortController();

  public async initialize(context: GameContext): Promise<void> {
    if (this.state === 'disposed') throw new Error('El juego ya fue liberado con dispose()');
    const mode = context?.mode ?? 'match';
    if (context?.gameType !== 'trivia' || !context.currentUser?.id || (mode === 'match' && !context.matchId))
      throw new Error('Contexto inválido: falta matchId, gameType trivia o currentUser');
    if (this.state !== 'created') throw new Error('El juego ya fue inicializado');
    if (mode === 'match' && !context.participants?.some(player => player.userId === context.currentUser.id))
      throw new Error('El Shell debe entregar los jugadores de la sala.');
    this.context = context;
    this.mode = mode;
    this.state = 'initialized';
  }
  public get soloTitle(): string { return this.mode === 'practice' ? 'Modo práctica' : 'Modo playground'; }
  private live(): boolean { return this.state !== 'disposed'; }
  private async join(): Promise<void> {
    const context = this.context;
    if (!context || !this.live()) return;
    await this.client!.joinMatch({ matchId: context.matchId, userId: context.currentUser.id, participants: context.participants ?? [] });
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
      client.on('GameFinished', data => { this.phase = 'finished'; this.result = data; this.notifyFinished(); }),
      client.on('GameError', data => { this.error = data.message; }),
      client.onConnectionStateChanged(state => {
        this.connection = state;
        if (state === 'connected' && this.joined && this.live()) void this.join().catch(() => {
          if (this.live()) this.error = this.solo
            ? 'No se pudo recuperar la partida. Empieza una nueva.'
            : 'No se pudo recuperar la partida. Reabre el juego desde Salas.';
        });
      }),
    ];
  }
  // R-46: el juego solo avisa al Shell; es el Shell quien notifica a Matchmaking. Sin sala no hay nada que avisar.
  private notifyFinished(): void {
    if (this.mode !== 'match' || !this.context) return;
    const detail: GameFinishedDetail = { matchId: this.context.matchId, gameType: this.context.gameType };
    this.context.events?.dispatchEvent(new CustomEvent(GAME_FINISHED_EVENT, { detail }));
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
    if (this.client) { this.state = 'running'; return; }
    if (this.mode === 'match') { await this.connect(new SignalRTriviaClient(apiUrl)); return; }
    this.state = 'running';
    if (this.mode === 'practice') await this.playSolo();
  }
  /** Modos práctica y playground: una partida de un solo jugador con las mismas pantallas. */
  public async playSolo(): Promise<void> {
    if (this.mode === 'match' || !this.context || this.connecting || (this.state !== 'initialized' && this.state !== 'running')) return;
    await this.leave();
    this.phase = 'waiting'; this.players = []; this.question = null; this.review = null; this.result = null;
    this.selectedAnswerId = null; this.answered = false; this.submitting = false; this.seconds = 0; this.error = '';
    this.connection = 'disconnected'; this.solo = true;
    try { await this.connect(new PracticeTriviaClient(apiUrl, this.context.currentUser)); }
    catch (error) {
      if (!this.live()) return;
      this.solo = false;
      this.error = error instanceof Error ? error.message : 'No se pudo iniciar la partida.';
    }
  }
  private async leave(): Promise<void> {
    clearInterval(this.timer); this.timer = undefined; this.joined = false;
    this.subscriptions.forEach(off => off()); this.subscriptions = [];
    const client = this.client; this.client = undefined;
    await client?.disconnect();
  }
  private async connect(client: TriviaHubClient): Promise<void> {
    this.connecting = true;
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
    if (!context || this.mode !== 'match' || this.loadingHistory || !this.live()) return;
    this.loadingHistory = true; this.error = '';
    try {
      const base = `${apiUrl}/api/games/trivia/players/${encodeURIComponent(context.currentUser.id)}`;
      const responses = await Promise.all(['history', 'stats'].map(path => fetch(`${base}/${path}`, { signal: this.abort.signal })));
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
