import { createFixture } from '@aurelia/testing';
import { tasksSettled } from '@aurelia/runtime';
import { DevShell } from '../../../src/trivia-frontend/src/dev/dev-shell';

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
