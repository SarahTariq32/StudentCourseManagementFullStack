import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../environments/environment';

export interface PendingRequestNotification {
  studentId: number;
  courseId: number;
  requestType: string;
  reason?: string;
}

export interface ProcessedRequestNotification {
  requestId: number;
  approve: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection!: signalR.HubConnection;

  // RxJS Subjects to emit live events to subscribers
  public pendingRequestCreated$ = new Subject<PendingRequestNotification>();
  public pendingRequestProcessed$ = new Subject<ProcessedRequestNotification>();

  public startConnection(token: string): void {
    // Prevent duplicate connections if already connected/connecting
    if (
      this.hubConnection &&
      (this.hubConnection.state === signalR.HubConnectionState.Connected ||
       this.hubConnection.state === signalR.HubConnectionState.Connecting)
    ) {
      return;
    }

    // Build WebSocket hub connection with JWT token passed via query string
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiUrl.replace('/api', '')}/hubs/admin`, {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection
      .start()
      .then(() => console.log('SignalR Admin Hub connected successfully'))
      .catch((err) => console.error('Error establishing SignalR connection:', err));

    // Listen for live backend events
    this.hubConnection.on('PendingRequestCreated', (data: PendingRequestNotification) => {
      this.pendingRequestCreated$.next(data);
    });

    this.hubConnection.on('PendingRequestProcessed', (data: ProcessedRequestNotification) => {
      this.pendingRequestProcessed$.next(data);
    });
  }

  public stopConnection(): void {
    if (this.hubConnection) {
      this.hubConnection.stop()
        .then(() => console.log('SignalR connection stopped'))
        .catch((err) => console.error('Error stopping SignalR connection:', err));
    }
  }
}