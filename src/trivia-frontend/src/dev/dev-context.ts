import type { GameContext } from '../contracts/game-contracts';

/**
 * Contexto simulado para el modo desarrollo. Se puede cambiar por la URL para abrir
 * varias pestañas con jugadores distintos:
 *   http://localhost:4002/?matchId=match-dev-001&userId=user-dev-2&displayName=Jugador%202
 */
export function readDevContext(search: string): GameContext {
  const params = new URLSearchParams(search);
  return {
    matchId: params.get('matchId') || 'match-dev-001',
    gameType: 'trivia',
    currentUser: {
      id: params.get('userId') || 'user-dev-1',
      displayName: params.get('displayName') || 'Jugador 1',
    },
  };
}
