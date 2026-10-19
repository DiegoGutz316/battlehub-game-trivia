// Arranque en modo independiente (npm start): main.ts carga bootstrap.ts, que inicia el arnés DevShell.
import { DevShell } from '../src/dev/dev-shell';

const mockStart = jest.fn();
const mockApp = jest.fn((component: unknown) => { void component; return { start: mockStart }; });
jest.mock('aurelia', () => ({
  ...jest.requireActual('aurelia'),
  __esModule: true,
  default: { app: (component: unknown) => mockApp(component) },
}));

it('main.ts carga bootstrap.ts de forma asíncrona y arranca DevShell', async () => {
  await import('../src/main');
  await new Promise(resolve => setTimeout(resolve, 0));

  expect(mockApp).toHaveBeenCalledWith(DevShell);
  expect(mockStart).toHaveBeenCalledTimes(1);
});
