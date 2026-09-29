import {
  ServerClock,
  computeOffsetMs,
  computeProgress,
  computeRemainingMs,
  isExpired,
  parseUtc,
  toDisplaySeconds,
} from '../../../src/trivia-frontend/src/services/trivia-hub/server-clock';

// Hora real del servidor al enviar QuestionStarted; la pregunta vence 15 s después.
const SERVER_TIME = '2026-09-27T05:00:00Z';
const EXPIRES_AT = '2026-09-27T05:00:15Z';
const SERVER_MS = Date.parse(SERVER_TIME);

describe('server-clock', () => {
  it('con el reloj local adelantado 3 s muestra el tiempo real', () => {
    const localNow = SERVER_MS + 3_000;
    const offset = computeOffsetMs(SERVER_TIME, localNow);

    expect(offset).toBe(-3_000);
    expect(computeRemainingMs(EXPIRES_AT, localNow, offset)).toBe(15_000);
  });

  it('con el reloj local atrasado muestra el tiempo real', () => {
    const localNow = SERVER_MS - 4_500;
    const offset = computeOffsetMs(SERVER_TIME, localNow);

    expect(offset).toBe(4_500);
    expect(computeRemainingMs(EXPIRES_AT, localNow, offset)).toBe(15_000);
    // 2 s después en el reloj local quedan 13 s reales.
    expect(computeRemainingMs(EXPIRES_AT, localNow + 2_000, offset)).toBe(13_000);
  });

  it('con 7,9 s restantes muestra 7 (redondeo hacia abajo)', () => {
    expect(toDisplaySeconds(7_900)).toBe(7);
  });

  it('a la mitad del tiempo la barra está en 0,5', () => {
    expect(computeProgress(7_500)).toBe(0.5);
    expect(computeProgress(2_500, 5_000)).toBe(0.5);
  });

  it('con el tiempo vencido muestra 0 segundos, progreso 0 y lo marca como vencido', () => {
    const remaining = computeRemainingMs(EXPIRES_AT, SERVER_MS + 20_000, 0);

    expect(remaining).toBe(0);
    expect(toDisplaySeconds(remaining)).toBe(0);
    expect(computeProgress(remaining)).toBe(0);
    expect(isExpired(remaining)).toBe(true);
  });

  it('lanza un error con una fecha inválida', () => {
    expect(() => parseUtc('no-es-una-fecha')).toThrow('Fecha UTC inválida');
    expect(() => parseUtc('2026-13-45T99:00:00Z')).toThrow('Fecha UTC inválida');
  });

  it('lanza un error con una fecha sin zona horaria', () => {
    expect(() => parseUtc('2026-09-27T05:00:00')).toThrow('Fecha UTC inválida');
  });

  it('acepta el desfase +00:00 como UTC', () => {
    expect(parseUtc('2026-09-27T05:00:00+00:00')).toBe(SERVER_MS);
  });

  it('ServerClock aplica el desfase guardado', () => {
    let localNow = SERVER_MS + 3_000;
    const clock = new ServerClock(() => localNow);

    clock.sync(SERVER_TIME);
    expect(clock.offsetMs).toBe(-3_000);
    expect(clock.now()).toBe(SERVER_MS);

    localNow += 7_100;
    expect(clock.countdown(EXPIRES_AT)).toEqual({
      remainingMs: 7_900,
      seconds: 7,
      progress: 7_900 / 15_000,
      expired: false,
    });
  });
});
