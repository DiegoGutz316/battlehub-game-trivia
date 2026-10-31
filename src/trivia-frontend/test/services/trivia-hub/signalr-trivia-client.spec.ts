import { SignalRTriviaClient } from '../../../src/services/trivia-hub/signalr-trivia-client';
import type { HubConnectionState } from '../../../src/services/trivia-hub/trivia-hub-client';

type Callback = () => void;
const mockLifecycle: { reconnecting?: Callback; reconnected?: Callback; close?: Callback } = {};
const mockConnection = {
  start: jest.fn(async () => undefined),
  stop: jest.fn(async () => undefined),
  invoke: jest.fn(async () => undefined),
  on: jest.fn(),
  off: jest.fn(),
  onreconnecting: jest.fn((callback: Callback) => { mockLifecycle.reconnecting = callback; }),
  onreconnected: jest.fn((callback: Callback) => { mockLifecycle.reconnected = callback; }),
  onclose: jest.fn((callback: Callback) => { mockLifecycle.close = callback; }),
};
const mockBuilder = {
  withUrl: jest.fn(() => mockBuilder),
  withAutomaticReconnect: jest.fn(() => mockBuilder),
  configureLogging: jest.fn(() => mockBuilder),
  build: jest.fn(() => mockConnection),
};
jest.mock('@microsoft/signalr', () => ({ HubConnectionBuilder: jest.fn(() => mockBuilder), LogLevel: { None: 6 } }));

function track(client: SignalRTriviaClient): HubConnectionState[] {
  const states: HubConnectionState[] = [];
  client.onConnectionStateChanged(state => states.push(state));
  return states;
}

beforeEach(() => { jest.clearAllMocks(); });

describe('SignalRTriviaClient', () => {
  it('se conecta a /hubs/trivia sin token y con reconexión automática', () => {
    new SignalRTriviaClient('http://localhost:5185');

    expect(mockBuilder.withUrl).toHaveBeenCalledWith('http://localhost:5185/hubs/trivia');
    expect(mockBuilder.withAutomaticReconnect).toHaveBeenCalledWith([0, 2000, 5000, 10000]);
    expect(mockBuilder.configureLogging).toHaveBeenCalledWith(6);
  });

  it('connect() pasa por connecting y connected', async () => {
    const client = new SignalRTriviaClient('http://api');
    const states = track(client);

    await client.connect();

    expect(states).toEqual(['connecting', 'connected']);
    expect(client.connectionState).toBe('connected');
  });

  it('si el servidor no responde, connect() queda en disconnected y lanza un error legible', async () => {
    mockConnection.start.mockRejectedValueOnce(new Error('ECONNREFUSED'));
    const client = new SignalRTriviaClient('http://api');
    const states = track(client);

    await expect(client.connect()).rejects.toThrow('No se pudo conectar con Trivia');
    expect(states).toEqual(['connecting', 'disconnected']);
  });

  it('informa reconexión, recuperación y cierre de la conexión', () => {
    const client = new SignalRTriviaClient('http://api');
    const states = track(client);

    mockLifecycle.reconnecting?.();
    mockLifecycle.reconnected?.();
    mockLifecycle.close?.();

    expect(states).toEqual(['reconnecting', 'connected', 'disconnected']);
  });

  it('invoca JoinMatch y SubmitAnswer con el contrato del hub', async () => {
    const client = new SignalRTriviaClient('http://api');

    const room = { matchId: 'm1', userId: 'one', participants: [{ userId: 'one', displayName: 'Uno' }] };
    await client.joinMatch(room);
    await client.submitAnswer({ matchId: 'm1', questionId: 'q1', answerId: 'a' });

    expect(mockConnection.invoke).toHaveBeenNthCalledWith(1, 'JoinMatch', room);
    expect(mockConnection.invoke).toHaveBeenNthCalledWith(2, 'SubmitAnswer', { matchId: 'm1', questionId: 'q1', answerId: 'a' });
  });

  it('on() registra el evento y la función devuelta lo quita', () => {
    const client = new SignalRTriviaClient('http://api');
    const handler = jest.fn();

    const off = client.on('GameFinished', handler);
    off();

    expect(mockConnection.on).toHaveBeenCalledWith('GameFinished', handler);
    expect(mockConnection.off).toHaveBeenCalledWith('GameFinished', handler);
  });

  it('deja de avisar a quien canceló su suscripción y disconnect() limpia a todos', async () => {
    const client = new SignalRTriviaClient('http://api');
    const cancelled = jest.fn();
    const kept = jest.fn();
    const unsubscribe = client.onConnectionStateChanged(cancelled);
    client.onConnectionStateChanged(kept);

    unsubscribe();
    mockLifecycle.reconnecting?.();
    await client.disconnect();
    mockLifecycle.close?.();

    expect(cancelled).not.toHaveBeenCalled();
    expect(kept).toHaveBeenCalledTimes(1);
    expect(mockConnection.stop).toHaveBeenCalledTimes(1);
  });
});
