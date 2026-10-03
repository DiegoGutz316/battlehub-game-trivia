// Arnés de desarrollo: simula lo que hace el Shell para probar el juego solo (npm start).
// No forma parte de ./GameModule, así que el Shell nunca lo carga.
import { GameModule } from '../game-module';
import type { GameContext } from '../contracts/game-contracts';
import { readDevContext } from './dev-context';

export class DevShell {
  public static dependencies = [GameModule];
  public game?: GameModule;
  public readonly context: GameContext = readDevContext(window.location.search);
  /** Mensaje del último rechazo de initialize()/start(); el Shell real lo mostraría como LIFECYCLE_ERROR. */
  public error = '';

  public async attached(): Promise<void> {
    await this.run(game => game.initialize(this.context));
  }

  public start(): Promise<void> {
    return this.run(game => game.start());
  }

  public pause(): Promise<void> {
    return this.run(game => game.pause());
  }

  public dispose(): Promise<void> {
    return this.run(game => game.dispose());
  }

  private async run(action: (game: GameModule) => Promise<void>): Promise<void> {
    this.error = '';
    if (!this.game) {
      return;
    }
    try {
      await action(this.game);
    } catch (error) {
      this.error = error instanceof Error ? error.message : String(error);
    }
  }
}
