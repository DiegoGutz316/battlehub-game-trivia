// Pruebas del ciclo de vida del contrato GameModule (ADR-003 y 03-contratos-tecnicos.md §6).
import { createFixture } from '@aurelia/testing';
import { tasksSettled } from '@aurelia/runtime';
import { GameModule } from '../../src/trivia-frontend/src/game-module';
import type { GameContext } from '../../src/trivia-frontend/src/contracts/game-contracts';

const ctx: GameContext = {
  matchId: 'match-001',
  gameType: 'trivia',
  currentUser: { id: 'user-001', displayName: 'Francisco' },
};

describe('GameModule (contrato ADR-003)', () => {
  it('exporta los 4 métodos del contrato', () => {
    const g = new GameModule();
    for (const m of ['initialize', 'start', 'pause', 'dispose'] as const) {
      expect(typeof g[m]).toBe('function');
    }
  });

  it('recorre el ciclo initialize → start → pause → dispose', async () => {
    const g = new GameModule();
    await g.initialize(ctx);
    expect(g.state).toBe('initialized');
    await g.start();
    expect(g.state).toBe('running');
    await g.pause();
    expect(g.state).toBe('paused');
    await g.dispose();
    expect(g.state).toBe('disposed');
  });

  it('initialize() guarda el contexto sin iniciar la partida', async () => {
    const g = new GameModule();
    await g.initialize(ctx);

    expect(g.context).toBe(ctx);
    expect(g.state).not.toBe('running');
  });

  it('start() reanuda una partida pausada', async () => {
    const g = new GameModule();
    await g.initialize(ctx);
    await g.start();
    await g.pause();

    await g.start();

    expect(g.state).toBe('running');
  });

  it('rechaza initialize() con un contexto inválido (LIFECYCLE_ERROR en el Shell)', async () => {
    const g = new GameModule();
    await expect(g.initialize({} as GameContext)).rejects.toThrow('Contexto inválido');
    expect(g.state).toBe('created');
  });

  it('rechaza start() si no se llamó antes a initialize()', async () => {
    const g = new GameModule();
    await expect(g.start()).rejects.toThrow('initialize()');
    expect(g.state).toBe('created');
  });

  it('después de dispose() rechaza initialize() y start()', async () => {
    const g = new GameModule();
    await g.initialize(ctx);
    await g.dispose();

    await expect(g.initialize(ctx)).rejects.toThrow('dispose()');
    await expect(g.start()).rejects.toThrow('dispose()');
    expect(g.state).toBe('disposed');
  });

  it('pause() solo tiene efecto con la partida en curso', async () => {
    const g = new GameModule();
    await g.initialize(ctx);

    await g.pause();

    expect(g.state).toBe('initialized');
  });

  it('se dibuja como <trivia-game-module> y muestra el contexto recibido', async () => {
    const { component, getBy } = await createFixture(
      '<trivia-game-module component.ref="game"></trivia-game-module>',
      class { public game!: GameModule; },
      [GameModule],
    ).started;

    await component.game.initialize(ctx);
    await component.game.start();
    await tasksSettled();

    expect(getBy('[data-testid="match-id"]')?.textContent).toBe('match-001');
    expect(getBy('[data-testid="player-name"]')?.textContent).toBe('Francisco');
    expect(getBy('[data-testid="game-state"]')?.textContent).toBe('running');
  });
});
