// Tipos del contrato 03-contratos-tecnicos.md (secciones 5 y 6). Copia idéntica en el Shell y en los juegos.
export type GameMode = 'match' | 'practice' | 'playground';

export interface GameContext {
  /** Sala de Matchmaking. Vacío en los modos sin sala. */
  matchId: string;
  gameType: 'typing' | 'trivia' | 'memory';
  currentUser: { id: string; displayName: string };
  getAccessToken?: () => Promise<string>;
  getMatchmakingAccessToken?: () => Promise<string>;
  /** Sin valor es una partida: 'practice' se abre desde el Shell sin sala y 'playground' se abre sin el Shell. */
  mode?: GameMode;
}

export interface GameModule {
  initialize(context: GameContext): Promise<void>;
  start(): Promise<void>;
  pause(): Promise<void>;
  dispose(): Promise<void>;
}
