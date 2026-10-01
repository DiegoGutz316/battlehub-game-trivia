import { readDevContext } from '../../../src/trivia-frontend/src/dev/dev-context';

describe('readDevContext', () => {
  it('sin parámetros usa la partida y el jugador por defecto', () => {
    expect(readDevContext('')).toEqual({
      matchId: 'match-dev-001',
      gameType: 'trivia',
      currentUser: { id: 'user-dev-1', displayName: 'Jugador 1' },
    });
  });

  it('toma la partida y el jugador de la URL', () => {
    const context = readDevContext('?matchId=match-7&userId=user-dev-2&displayName=Jugador%202');

    expect(context.matchId).toBe('match-7');
    expect(context.currentUser).toEqual({ id: 'user-dev-2', displayName: 'Jugador 2' });
  });

  it('un parámetro vacío no reemplaza el valor por defecto', () => {
    expect(readDevContext('?userId=').currentUser.id).toBe('user-dev-1');
  });
});
