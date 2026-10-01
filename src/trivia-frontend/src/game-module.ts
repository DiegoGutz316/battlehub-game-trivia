// ADR-003 §2 — Módulo que el Shell carga. Debe exportarse con el nombre "GameModule"
// e implementar los 4 métodos del contrato (03-contratos-tecnicos.md, sección 6).
import { customElement } from 'aurelia';
import template from './game-module.html';
import './game-module.css';
import type { GameContext, GameModule as IGameModule } from './contracts/game-contracts';

export type GameState = 'created' | 'initialized' | 'running' | 'paused' | 'disposed';

@customElement({ name: 'trivia-game-module', template })
export class GameModule implements IGameModule {
  public context: GameContext | null = null;
  public state: GameState = 'created';

  /** Recibe el contexto del Shell y prepara el estado inicial. NO inicia la partida. */
  public async initialize(context: GameContext): Promise<void> {
    // ADR-003 §6: si el juego no puede iniciar, RECHAZAR la promesa. El Shell lo mostrará como LIFECYCLE_ERROR.
    if (this.state === 'disposed') {
      throw new Error('El juego ya fue liberado con dispose()');
    }
    if (!context?.matchId || !context.currentUser?.id) {
      throw new Error('Contexto inválido: falta matchId o currentUser');
    }
    this.context = context;
    this.state = 'initialized';
  }

  /** Comienza o reanuda la partida. En E3 aquí se conecta al hub /hubs/trivia. */
  public async start(): Promise<void> {
    if (this.state === 'disposed') {
      throw new Error('El juego ya fue liberado con dispose()');
    }
    if (!this.context) {
      throw new Error('El juego no fue inicializado: falta llamar a initialize()');
    }
    this.state = 'running';
  }

  /** Pausa (el usuario salió del área del juego). El Shell puede volver a llamar start(). */
  public async pause(): Promise<void> {
    if (this.state === 'running') {
      this.state = 'paused';
    }
  }

  /** Libera todo antes de que el Shell descargue el juego. En E3 aquí se cierra la conexión al hub. */
  public async dispose(): Promise<void> {
    this.state = 'disposed';
  }
}
