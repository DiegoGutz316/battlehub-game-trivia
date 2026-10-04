import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { TriviaHubClient, HubConnectionState, HubEventHandler, Unsubscribe } from './trivia-hub-client';
import type { JoinMatchRequest, SubmitAnswerRequest, TriviaHubEventName } from './contract';

export class SignalRTriviaClient implements TriviaHubClient {
  private readonly connection;
  private readonly listeners = new Set<(state: HubConnectionState) => void>();
  public connectionState: HubConnectionState = 'disconnected';

  public constructor(url: string, token: () => Promise<string>) {
    this.connection = new HubConnectionBuilder().withUrl(`${url}/hubs/trivia`, { accessTokenFactory: token })
      .withAutomaticReconnect([0, 2000, 5000, 10000]).configureLogging(LogLevel.None).build();
    this.connection.onreconnecting(() => this.update('reconnecting'));
    this.connection.onreconnected(() => this.update('connected'));
    this.connection.onclose(() => this.update('disconnected'));
  }
  private update(state: HubConnectionState): void {
    this.connectionState = state;
    this.listeners.forEach(listener => listener(state));
  }
  public async connect(): Promise<void> {
    this.update('connecting');
    try { await this.connection.start(); this.update('connected'); }
    catch { this.update('disconnected'); throw new Error('No se pudo conectar con Trivia. Revisa el backend y la autorización.'); }
  }
  public async disconnect(): Promise<void> { await this.connection.stop(); this.listeners.clear(); }
  public async joinMatch(request: JoinMatchRequest): Promise<void> { await this.connection.invoke('JoinMatch', request); }
  public async submitAnswer(request: SubmitAnswerRequest): Promise<void> { await this.connection.invoke('SubmitAnswer', request); }
  public on<E extends TriviaHubEventName>(event: E, handler: HubEventHandler<E>): Unsubscribe {
    this.connection.on(event, handler);
    return () => this.connection.off(event, handler);
  }
  public onConnectionStateChanged(handler: (state: HubConnectionState) => void): Unsubscribe {
    this.listeners.add(handler);
    return () => this.listeners.delete(handler);
  }
}
