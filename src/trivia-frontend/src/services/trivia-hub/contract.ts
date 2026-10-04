// Tipos del contrato del hub de Trivia Battle.
// Fuente de verdad: docs/contrato-signalr-trivia.md y docs/reglas-trivia.md.
// Este archivo no contiene lógica del juego.

/** Fecha y hora en UTC, formato ISO-8601 con sufijo `Z`. */
export type UtcDateString = string;

export const TRIVIA_HUB_PATH = '/hubs/trivia';

export const TRIVIA_RULES = {
  totalQuestions: 10,
  optionsPerQuestion: 4,
  questionDurationMs: 15_000,
  reviewDurationMs: 5_000,
  basePoints: 100,
  pointsPerRemainingSecond: 10,
  maxPointsPerQuestion: 240,
} as const;

export type GamePhase = 'waiting' | 'playing' | 'reviewing' | 'finished';

// ===== Estructuras comunes =====

export interface Player {
  userId: string;
  displayName: string;
  score: number;
  isConnected: boolean;
}

export interface AnswerOption {
  answerId: string;
  text: string;
}

/** No incluye la respuesta correcta. */
export interface Question {
  questionId: string;
  questionNumber: number;
  totalQuestions: number;
  text: string;
  category: string | null;
  options: AnswerOption[];
  expiresAtUtc: UtcDateString;
}

/** Pregunta activa dentro de `CurrentState`. */
export interface CurrentQuestion extends Question {
  hasAnswered: boolean;
  selectedAnswerId: string | null;
}

// ===== Cliente → Servidor =====

export const TRIVIA_HUB_METHODS = {
  joinMatch: 'JoinMatch',
  submitAnswer: 'SubmitAnswer',
} as const;

export interface JoinMatchRequest {
  matchId: string;
  matchmakingAccessToken?: string;
}

export interface SubmitAnswerRequest {
  matchId: string;
  questionId: string;
  answerId: string;
}

// ===== Servidor → Cliente =====

export interface QuestionClosedEvent {
  questionId: string;
  correctAnswerId: string;
  selectedAnswerId: string | null;
  isCorrect: boolean;
  pointsEarned: number;
  totalScore: number;
  nextQuestionAtUtc: UtcDateString | null;
  serverTimeUtc: UtcDateString;
}

export interface CurrentStateEvent {
  matchId: string;
  phase: GamePhase;
  serverTimeUtc: UtcDateString;
  totalQuestions: number;
  players: Player[];
  /** Solo cuando `phase` es `playing`. */
  currentQuestion: CurrentQuestion | null;
  /** Solo cuando `phase` es `reviewing`. */
  lastQuestionResult: QuestionClosedEvent | null;
}

export interface PlayerJoinedEvent {
  userId: string;
  displayName: string;
}

export interface PlayerDisconnectedEvent {
  userId: string;
  displayName: string;
}

export interface PlayerReconnectedEvent {
  userId: string;
  displayName: string;
}

export interface GameStartedEvent {
  matchId: string;
  totalQuestions: number;
  startedAtUtc: UtcDateString;
  serverTimeUtc: UtcDateString;
}

export interface QuestionStartedEvent extends Question {
  serverTimeUtc: UtcDateString;
}

export interface AnswerReceivedEvent {
  questionId: string;
  answerId: string;
  receivedAtUtc: UtcDateString;
}

export interface ScoreboardUpdatedEvent {
  /** Ordenada de mayor a menor puntaje. */
  players: Player[];
}

export interface FinalPlayerResult {
  userId: string;
  displayName: string;
  score: number;
  correctAnswers: number;
  correctAnswersTimeMs: number;
  /** Los jugadores empatados de forma definitiva comparten la misma posición. */
  position: number;
}

export interface GameFinishedEvent {
  matchId: string;
  players: FinalPlayerResult[];
  winnerUserId: string | null;
  isTie: boolean;
  decidedByTiebreak: boolean;
  finishedAtUtc: UtcDateString;
}

export type GameErrorCode =
  | 'MATCH_NOT_FOUND'
  | 'PLAYER_NOT_IN_MATCH'
  | 'MATCH_FINISHED'
  | 'UNAUTHORIZED'
  | 'QUESTION_NOT_ACTIVE'
  | 'QUESTION_CLOSED'
  | 'ALREADY_ANSWERED'
  | 'INVALID_ANSWER';

export interface GameErrorEvent {
  code: GameErrorCode;
  message: string;
}

export interface TriviaHubEventMap {
  CurrentState: CurrentStateEvent;
  PlayerJoined: PlayerJoinedEvent;
  PlayerDisconnected: PlayerDisconnectedEvent;
  PlayerReconnected: PlayerReconnectedEvent;
  GameStarted: GameStartedEvent;
  QuestionStarted: QuestionStartedEvent;
  AnswerReceived: AnswerReceivedEvent;
  QuestionClosed: QuestionClosedEvent;
  ScoreboardUpdated: ScoreboardUpdatedEvent;
  GameFinished: GameFinishedEvent;
  GameError: GameErrorEvent;
}

export type TriviaHubEventName = keyof TriviaHubEventMap;

export const TRIVIA_HUB_EVENTS = [
  'CurrentState',
  'PlayerJoined',
  'PlayerDisconnected',
  'PlayerReconnected',
  'GameStarted',
  'QuestionStarted',
  'AnswerReceived',
  'QuestionClosed',
  'ScoreboardUpdated',
  'GameFinished',
  'GameError',
] as const satisfies readonly TriviaHubEventName[];

// Falla al compilar si falta en la lista algún evento del mapa.
type MissingHubEvents = Exclude<TriviaHubEventName, (typeof TRIVIA_HUB_EVENTS)[number]>;
const allHubEventsListed: [MissingHubEvents] extends [never] ? true : never = true;
void allHubEventsListed;
