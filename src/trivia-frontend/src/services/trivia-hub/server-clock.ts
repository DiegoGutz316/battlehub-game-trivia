import { TRIVIA_RULES, type UtcDateString } from './contract';

// Acepta `Z` (lo que pide el contrato) y un desfase explícito como `+00:00`.
// Sin zona horaria, JavaScript interpretaría la fecha como hora local.
const ISO_UTC_PATTERN = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$/;

/** Convierte una fecha ISO-8601 con zona horaria a milisegundos. */
export function parseUtc(value: UtcDateString): number {
  const ms = ISO_UTC_PATTERN.test(value) ? Date.parse(value) : NaN;
  if (Number.isNaN(ms)) {
    throw new Error(`Fecha UTC inválida: "${value}"`);
  }
  return ms;
}

/**
 * Desfase = serverTimeUtc − reloj local.
 * Incluye la latencia de ida del evento, que para este juego es aceptable.
 */
export function computeOffsetMs(serverTimeUtc: UtcDateString, localNowMs: number): number {
  return parseUtc(serverTimeUtc) - localNowMs;
}

/** Tiempo restante = expiresAtUtc − (reloj local + desfase). Nunca es negativo. */
export function computeRemainingMs(
  expiresAtUtc: UtcDateString,
  localNowMs: number,
  offsetMs: number,
): number {
  return Math.max(0, parseUtc(expiresAtUtc) - (localNowMs + offsetMs));
}

/** Segundos completos restantes, redondeados hacia abajo como en el cálculo del bonus. */
export function toDisplaySeconds(remainingMs: number): number {
  return Math.max(0, Math.floor(remainingMs / 1000));
}

/** Progreso de la barra: 1 al empezar, 0 al vencer. */
export function computeProgress(
  remainingMs: number,
  totalDurationMs: number = TRIVIA_RULES.questionDurationMs,
): number {
  if (totalDurationMs <= 0) {
    return 0;
  }
  return Math.min(1, Math.max(0, remainingMs / totalDurationMs));
}

export function isExpired(remainingMs: number): boolean {
  return remainingMs <= 0;
}

export interface Countdown {
  remainingMs: number;
  seconds: number;
  progress: number;
  expired: boolean;
}

/**
 * Guarda el desfase con el servidor y calcula el tiempo restante.
 * Siempre recalcula desde expiresAtUtc; nunca descuenta de a un segundo.
 */
export class ServerClock {
  private offset = 0;

  public constructor(private readonly localNow: () => number = () => Date.now()) {}

  public get offsetMs(): number {
    return this.offset;
  }

  /** Se llama con el serverTimeUtc de cada evento que lo incluya. */
  public sync(serverTimeUtc: UtcDateString): void {
    this.offset = computeOffsetMs(serverTimeUtc, this.localNow());
  }

  /** Hora estimada del servidor, en milisegundos. */
  public now(): number {
    return this.localNow() + this.offset;
  }

  public countdown(
    expiresAtUtc: UtcDateString,
    totalDurationMs: number = TRIVIA_RULES.questionDurationMs,
  ): Countdown {
    const remainingMs = computeRemainingMs(expiresAtUtc, this.localNow(), this.offset);
    return {
      remainingMs,
      seconds: toDisplaySeconds(remainingMs),
      progress: computeProgress(remainingMs, totalDurationMs),
      expired: isExpired(remainingMs),
    };
  }
}
