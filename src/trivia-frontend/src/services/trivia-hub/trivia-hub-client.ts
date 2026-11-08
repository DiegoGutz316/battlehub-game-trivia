import type { JoinMatchRequest, SubmitAnswerRequest, TriviaHubEventMap, TriviaHubEventName } from './contract';

export type HubConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';
export type Unsubscribe = () => void;
export type HubEventHandler<E extends TriviaHubEventName> = (data: TriviaHubEventMap[E]) => void;

export interface TriviaHubClient {
  readonly connectionState: HubConnectionState;
  connect(): Promise<void>;
  disconnect(): Promise<void>;
  joinMatch(request: JoinMatchRequest): Promise<void>;
  submitAnswer(request: SubmitAnswerRequest): Promise<void>;
  /** Solo las partidas de un solo jugador: detienen y reanudan el tiempo en el servidor. Una sala es compartida y no se pausa. */
  pause?(): Promise<void>;
  resume?(): Promise<void>;
  on<E extends TriviaHubEventName>(event: E, handler: HubEventHandler<E>): Unsubscribe;
  onConnectionStateChanged(handler: (state: HubConnectionState) => void): Unsubscribe;
}
