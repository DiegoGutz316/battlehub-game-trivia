// Tipos del contrato 03-contratos-tecnicos.md (secciones 5 y 6). GameContext, PracticeContext y GameLaunchContext coinciden con el Shell.
export type GameMode = 'match' | 'practice' | 'playground';

export interface GamePlayer {
  userId: string;
  displayName: string;
}

interface GameBaseContext {
  gameType: 'typing' | 'trivia' | 'memory';
  currentUser: { id: string; displayName: string };
  /** Canal local que el Shell crea para esta partida. No contiene tokens. */
  events?: EventTarget;
}

/** Partida en una sala de Matchmaking. */
export interface GameContext extends GameBaseContext {
  matchId: string;
  /** Jugadores de la sala, incluido currentUser. */
  participants: GamePlayer[];
  mode?: 'match';
}

/** El Shell abre el juego sin sala: no hay jugadores que esperar ni partida que registrar. */
export interface PracticeContext extends GameBaseContext {
  mode: 'practice';
  matchId?: never;
  participants?: never;
}

/** El juego abierto por sí solo, sin el Shell. */
export interface PlaygroundContext extends GameBaseContext {
  mode: 'playground';
  matchId?: never;
  participants?: never;
}

export type GameLaunchContext = GameContext | PracticeContext | PlaygroundContext;

export interface GameModule {
  initialize(context: GameLaunchContext): Promise<void>;
  start(): Promise<void>;
  pause(): Promise<void>;
  dispose(): Promise<void>;
}

/** El juego lo emite en context.events al terminar una partida; el Shell lo escucha y avisa a Matchmaking. */
export const GAME_FINISHED_EVENT = 'battlehub:game-finished';

export interface GameFinishedDetail {
  matchId: string;
  gameType: GameContext['gameType'];
}
