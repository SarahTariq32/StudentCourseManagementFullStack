import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { LoginRequest, RegisterRequest, AuthResponse } from '../models/auth.model';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  // 1. Centralized API endpoint property
  private apiUrl = `${environment.apiUrl}/Auth`;

  // 2. Constructor Injection of Angular's HttpClient
  constructor(private http: HttpClient) {}

  /**
   * 3. Login HTTP Call
   */
  login(credentials: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/login`, credentials);
  }

  /**
   * 4. Register HTTP Call
   */
  register(data: any): Observable<any> {
  return this.http.post(`${environment.apiUrl}/Auth/register`, data);
}

refreshToken(accessToken: string, refreshToken: string): Observable<any> {
  return this.http.post(`${environment.apiUrl}/Auth/refresh-token`, { accessToken, refreshToken });
}
}