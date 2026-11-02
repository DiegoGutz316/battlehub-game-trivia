import type { PlaygroundContext } from '../contracts/game-contracts';

/**
 * Contexto del modo playground: el juego abierto por sí solo, sin el Shell (R-34).
 * El nombre del jugador se puede cambiar por la URL:
 *   http://localhost:4002/?displayName=Jugador%202
 */
export function readDevContext(search: string): PlaygroundContext {
  const params = new URLSearchParams(search);
  return {
    gameType: 'trivia',
    mode: 'playground',
    currentUser: {
      id: params.get('userId') || 'user-dev-1',
      displayName: params.get('displayName') || 'Jugador 1',
    },
  };
}
