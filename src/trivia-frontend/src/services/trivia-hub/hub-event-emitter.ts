/** Reparte cada evento a quienes se suscribieron a él. */
export class HubEventEmitter<TMap extends object> {
  private readonly handlers = new Map<keyof TMap, Set<(data: never) => void>>();

  public on<K extends keyof TMap>(event: K, handler: (data: TMap[K]) => void): () => void {
    const set = this.handlers.get(event) ?? new Set();
    this.handlers.set(event, set);
    set.add(handler);

    return () => {
      set.delete(handler);
    };
  }

  public emit<K extends keyof TMap>(event: K, data: TMap[K]): void {
    const set = this.handlers.get(event);
    if (!set) {
      return;
    }
    // Copia: un handler puede cancelar su suscripción mientras se reparte el evento.
    for (const handler of [...set] as ((data: TMap[K]) => void)[]) {
      try {
        handler(data);
      } catch (error) {
        console.error(`Error en un handler del evento ${String(event)}`, error);
      }
    }
  }

  public clear(): void {
    this.handlers.clear();
  }
}
