import * as signalR from '@microsoft/signalr';
import type { ApiTaskResponse } from '../types';

// Używamy względnego URLa - nginx będzie proxy dla /ws
const WS_URL = window.location.origin;

class WebSocketService {
  private connection: signalR.HubConnection | null = null;
  private listeners: Map<string, (data: ApiTaskResponse) => void> = new Map();

  async connect(userId: string): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${WS_URL}/ws/notificationHub`)
      .withAutomaticReconnect()
      .build();

    this.connection.on('ApiResponse', (data: ApiTaskResponse) => {
      console.log('Received API response:', data);
      const listener = this.listeners.get(data.taskId);
      if (listener) {
        listener(data);
        this.listeners.delete(data.taskId);
      }
    });

    try {
      await this.connection.start();
      console.log('WebSocket connected');
      await this.connection.invoke('RegisterUser', userId);
      console.log('User registered:', userId);
    } catch (err) {
      console.error('WebSocket connection error:', err);
      throw err;
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.listeners.clear();
    }
  }

  onTaskComplete(taskId: string, callback: (data: ApiTaskResponse) => void): void {
    this.listeners.set(taskId, callback);
  }

  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

export const websocketService = new WebSocketService();
