// Tipos del contrato 03-contratos-tecnicos.md (secciones 5 y 6). Copia idéntica en el Shell y en los juegos.
export type GameMode = 'match' | 'practice' | 'playground';

export interface GamePlayer {
  userId: string;
  displayName: string;
}

export interface GameContext {
  /** Sala de Matchmaking. Vacío en los modos sin sala. */
  matchId: string;
  gameType: 'typing' | 'trivia' | 'memory';
  currentUser: { id: string; displayName: string };
  /** Jugadores de la sala, incluido currentUser. Obligatorio en una partida. */
  participants?: GamePlayer[];
  /** Canal local que el Shell crea para esta partida. No contiene tokens. */
  events?: EventTarget;
  /** Sin valor es una partida: 'practice' se abre desde el Shell sin sala y 'playground' se abre sin el Shell. */
  mode?: GameMode;
}

export interface GameModule {
  initialize(context: GameContext): Promise<void>;
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
