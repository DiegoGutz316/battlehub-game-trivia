import { BrowserPlatform } from '@aurelia/platform-browser';
import { setPlatform, onFixtureCreated, type IFixture } from '@aurelia/testing';

// Sets up the Aurelia environment for testing
function bootstrapTextEnv() {
  const platform = new BrowserPlatform(window);
  setPlatform(platform);
  BrowserPlatform.set(globalThis, platform);
}

const fixtures: IFixture<object>[] = [];
beforeAll(() => {
  bootstrapTextEnv();
  onFixtureCreated(fixture => {
    fixtures.push(fixture);
  });
});

// Espera a que cada fixture se detenga antes de pasar a la siguiente prueba.
afterEach(async () => {
  const pending = fixtures.splice(0, fixtures.length);
  for (const f of pending) {
    try {
      await f.stop(true);
    } catch {
      // ignore
    }
  }
});
