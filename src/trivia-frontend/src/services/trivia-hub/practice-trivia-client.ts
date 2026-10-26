// Cliente del modo práctica (hub anónimo /hubs/trivia/practice, un solo jugador, sin el Shell).
// Traduce el protocolo de práctica a los mismos eventos del hub de partidas, para que GameModule
// reutilice sus pantallas: pregunta, opciones, temporizador, revisión y resultado.
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr';
import type { TriviaHubClient, HubConnectionState, HubEventHandler, Unsubscribe } from './trivia-hub-client';
import type {
  GamePhase, Player, QuestionClosedEvent, QuestionStartedEvent, SubmitAnswerRequest, TriviaHubEventMap, TriviaHubEventName,
} from './contract';

interface PracticeStarted { matchId: string; secret: string; firstQuestion: QuestionStartedEvent }
interface PracticeAnswer { questionId: string; selectedAnswerId: string; receivedAtUtc: string }
interface PracticeFinished { matchId: string; totalScore: number; correctAnswers: number }
interface PracticeState extends PracticeFinished {
  phase: GamePhase;
  totalQuestions: number;
  currentQuestion: QuestionStartedEvent | null;
  review: QuestionClosedEvent | null;
  serverTimeUtc: string;
}

export class PracticeTriviaClient implements TriviaHubClient {
  private readonly connection: HubConnection;
  private readonly listeners = new Set<(state: HubConnectionState) => void>();
  private readonly handlers = new Map<TriviaHubEventName, Set<(data: never) => void>>();
  private session?: { matchId: string; secret: string };
  private lastQuestion?: QuestionStartedEvent;
  private lastAnswer?: { questionId: string; answerId: string };
  private score = 0;
  public connectionState: HubConnectionState = 'disconnected';

  public constructor(url: string, private readonly player: { id: string; displayName: string }) {
    this.connection = new HubConnectionBuilder().withUrl(`${url}/hubs/trivia/practice`)
      .withAutomaticReconnect([0, 2000, 5000, 10000]).configureLogging(LogLevel.None).build();
    this.connection.onreconnecting(() => this.update('reconnecting'));
    this.connection.onreconnected(() => this.update('connected'));
    this.connection.onclose(() => this.update('disconnected'));
    this.connection.on('QuestionStarted', (data: QuestionStartedEvent) => { this.lastQuestion = data; this.emit('QuestionStarted', data); });
    this.connection.on('QuestionClosed', (data: QuestionClosedEvent) => {
      this.score = data.totalScore;
      this.emit('QuestionClosed', data);
      this.emit('ScoreboardUpdated', { players: [this.me()] });
    });
    this.connection.on('GameFinished', (data: PracticeFinished) => this.finish(data));
  }
  private update(state: HubConnectionState): void {
    this.connectionState = state;
    this.listeners.forEach(listener => listener(state));
  }
  private emit<E extends TriviaHubEventName>(event: E, data: TriviaHubEventMap[E]): void {
    this.handlers.get(event)?.forEach(handler => handler(data as never));
  }
  private me(): Player {
    return { userId: this.player.id, displayName: this.player.displayName, score: this.score, isConnected: true };
  }
  private finish(data: PracticeFinished): void {
    this.score = data.totalScore;
    this.emit('GameFinished', {
      matchId: data.matchId, winnerUserId: this.player.id, isTie: false, decidedByTiebreak: false, finishedAtUtc: new Date().toISOString(),
      players: [{ userId: this.player.id, displayName: this.player.displayName, score: data.totalScore, correctAnswers: data.correctAnswers, correctAnswersTimeMs: 0, position: 1 }],
    });
  }
  public async connect(): Promise<void> {
    this.update('connecting');
    try { await this.connection.start(); this.update('connected'); }
    catch { this.update('disconnected'); throw new Error('No se pudo conectar con el modo práctica. Revisa que la API de Trivia esté encendida.'); }
  }
  public async disconnect(): Promise<void> { await this.connection.stop(); this.listeners.clear(); this.handlers.clear(); }
  /** La primera vez empieza la práctica; después de una reconexión la recupera con su clave. */
  public async joinMatch(): Promise<void> {
    if (!this.session) {
      let started: PracticeStarted;
      try { started = await this.connection.invoke<PracticeStarted>('StartPractice', this.player.displayName); }
      catch { throw new Error('No se pudo iniciar la práctica. Reintenta en unos segundos.'); }
      this.session = { matchId: started.matchId, secret: started.secret };
      this.lastQuestion = started.firstQuestion;
      this.emit('CurrentState', {
        matchId: started.matchId, phase: 'playing', serverTimeUtc: started.firstQuestion.serverTimeUtc,
        totalQuestions: started.firstQuestion.totalQuestions, players: [this.me()],
        currentQuestion: { ...started.firstQuestion, hasAnswered: false, selectedAnswerId: null }, lastQuestionResult: null,
      });
      return;
    }
    const state = await this.connection.invoke<PracticeState>('ReconnectPractice', this.session.matchId, this.session.secret);
    this.score = state.totalScore;
    // Durante la revisión el servidor no reenvía la pregunta: se conserva la última que se mostró.
    const question = state.currentQuestion ?? (state.review && this.lastQuestion?.questionId === state.review.questionId ? this.lastQuestion : null);
    const answerId = question && this.lastAnswer?.questionId === question.questionId ? this.lastAnswer.answerId : null;
    this.emit('CurrentState', {
      matchId: state.matchId, phase: state.phase, serverTimeUtc: state.serverTimeUtc, totalQuestions: state.totalQuestions,
      players: [this.me()], currentQuestion: question ? { ...question, hasAnswered: answerId !== null, selectedAnswerId: answerId } : null,
      lastQuestionResult: state.review,
    });
    if (state.phase === 'finished') this.finish(state);
  }
  public async submitAnswer(request: SubmitAnswerRequest): Promise<void> {
    if (!this.session) throw new Error('La práctica todavía no empezó.');
    const received = await this.connection.invoke<PracticeAnswer>(
      'SubmitPracticeAnswer', this.session.matchId, this.session.secret, request.questionId, request.answerId);
    this.lastAnswer = { questionId: received.questionId, answerId: received.selectedAnswerId };
    this.emit('AnswerReceived', { questionId: received.questionId, answerId: received.selectedAnswerId, receivedAtUtc: received.receivedAtUtc });
  }
  public on<E extends TriviaHubEventName>(event: E, handler: HubEventHandler<E>): Unsubscribe {
    const handlers = this.handlers.get(event) ?? new Set<(data: never) => void>();
    handlers.add(handler);
    this.handlers.set(event, handlers);
    return () => { handlers.delete(handler); };
  }
  public onConnectionStateChanged(handler: (state: HubConnectionState) => void): Unsubscribe {
    this.listeners.add(handler);
    return () => this.listeners.delete(handler);
  }
}
