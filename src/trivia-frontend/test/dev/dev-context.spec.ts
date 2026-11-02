import { readDevContext } from '../../src/dev/dev-context';

describe('readDevContext', () => {
  it('sin parámetros usa el jugador por defecto', () => {
    expect(readDevContext('')).toEqual({
      gameType: 'trivia',
      mode: 'playground',
      currentUser: { id: 'user-dev-1', displayName: 'Jugador 1' },
    });
  });

  it('toma el jugador de la URL y no crea una sala aunque traiga matchId', () => {
    const context = readDevContext('?matchId=match-7&userId=user-dev-2&displayName=Jugador%202');

    expect(context).not.toHaveProperty('matchId');
    expect(context.currentUser).toEqual({ id: 'user-dev-2', displayName: 'Jugador 2' });
  });

  it('un parámetro vacío no reemplaza el valor por defecto', () => {
    expect(readDevContext('?userId=').currentUser.id).toBe('user-dev-1');
  });
});
