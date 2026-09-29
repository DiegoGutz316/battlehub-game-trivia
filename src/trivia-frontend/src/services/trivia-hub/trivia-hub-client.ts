import type {
  JoinMatchRequest,
  SubmitAnswerRequest,
  TriviaHubEventMap,
  TriviaHubEventName,
} from './contract';

export type HubConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

/**
 * Identidad simulada del jugador. TEMPORAL hasta E4.
 * Según la sección "Identidad del jugador" del contrato, se envía como devUserId y
 * devDisplayName en la URL de conexión y solo se acepta en desarrollo. En E4 el
 * servidor obtendrá la identidad del JWT.
 */
export interface DevIdentity {
  userId: string;
  displayName: string;
}

export type Unsubscribe = () => void;

export type HubEventHandler<E extends TriviaHubEventName> = (data: TriviaHubEventMap[E]) => void;

/** Interfaz común del cliente simulado (E2) y del cliente real de SignalR (E3). */
export interface TriviaHubClient {
  readonly connectionState: HubConnectionState;

  connect(identity: DevIdentity): Promise<void>;

  disconnect(): Promise<void>;

  joinMatch(request: JoinMatchRequest): Promise<void>;

  submitAnswer(request: SubmitAnswerRequest): Promise<void>;

  on<E extends TriviaHubEventName>(event: E, handler: HubEventHandler<E>): Unsubscribe;

  onConnectionStateChanged(handler: (state: HubConnectionState) => void): Unsubscribe;
}
