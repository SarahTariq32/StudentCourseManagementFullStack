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

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection!: signalR.HubConnection;

  // RxJS Subject to emit live new request submission events
  public pendingRequestCreated$ = new Subject<PendingRequestNotification>();

  public startConnection(token: string): void {
    if (
      this.hubConnection &&
      (this.hubConnection.state === signalR.HubConnectionState.Connected ||
       this.hubConnection.state === signalR.HubConnectionState.Connecting)
    ) {
      return;
    }

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

    // Listen for live student request submissions
    this.hubConnection.on('PendingRequestCreated', (data: PendingRequestNotification) => {
      this.pendingRequestCreated$.next(data);
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