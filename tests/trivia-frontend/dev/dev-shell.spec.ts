import { createFixture } from '@aurelia/testing';
import { tasksSettled } from '@aurelia/runtime';
import { DevShell } from '../../../src/trivia-frontend/src/dev/dev-shell';

jest.mock('../../../src/trivia-frontend/src/services/trivia-hub/signalr-trivia-client', () => ({
  SignalRTriviaClient: jest.fn(() => ({
    connect: jest.fn(async () => undefined),
    disconnect: jest.fn(async () => undefined),
    joinMatch: jest.fn(async () => undefined),
    submitAnswer: jest.fn(async () => undefined),
    on: jest.fn(() => () => undefined),
    onConnectionStateChanged: jest.fn(() => () => undefined),
  })),
}));

async function renderDevShell() {
  const fixture = await createFixture(
    '<dev-shell component.ref="shell"></dev-shell>',
    class { public shell!: DevShell; },
    [DevShell],
  ).started;
  await tasksSettled();
  return { shell: fixture.component.shell, queryBy: fixture.queryBy.bind(fixture) };
}

describe('dev-shell (arnés de desarrollo)', () => {
  it('al montarse inicializa el juego con el contexto simulado, sin iniciarlo', async () => {
    const { shell } = await renderDevShell();

    expect(shell.game?.state).toBe('initialized');
    expect(shell.game?.context).toBe(shell.context);
  });

  it('los botones recorren start → pause → dispose', async () => {
    const { shell } = await renderDevShell();

    await shell.start();
    expect(shell.game?.state).toBe('running');
    await shell.pause();
    expect(shell.game?.state).toBe('paused');
    await shell.dispose();
    expect(shell.game?.state).toBe('disposed');
    expect(shell.error).toBe('');
  });

  it('muestra el error cuando el juego rechaza una llamada', async () => {
    const { shell, queryBy } = await renderDevShell();
    await shell.dispose();

    await shell.start();
    await tasksSettled();

    expect(shell.error).toContain('dispose()');
    expect(queryBy('[data-testid="dev-error"]')?.textContent).toContain('LIFECYCLE_ERROR');
  });
});
