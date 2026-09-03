import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { StudentProfile, VerificationResponse, UpdateStudentProfileRequest } from '../models/student.model';

@Injectable({
  providedIn: 'root'
})
export class StudentService {
  private apiUrl = `${environment.apiUrl}/Students`;

  constructor(private http: HttpClient) {}

  getMyProfile(): Observable<VerificationResponse> {
    return this.http.get<VerificationResponse>(`${this.apiUrl}/me`);
  }

  updateMyProfile(data: UpdateStudentProfileRequest): Observable<any> {
    return this.http.put(`${this.apiUrl}/me`, data);
  }

  requestAccountCreation(reason: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/request-enrollment`, { 
      courseId: 0, 
      reason: `ACCOUNT_CREATION_REQUEST: ${reason}` 
    });
  }

  enrollDirectly(courseId: number): Observable<any> {
    return this.http.post(`${this.apiUrl}/enroll`, { courseId });
  }

  requestEnrollment(courseId: number, reason: string, studentId?: number, courseName?: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/request-enrollment`, { courseId, studentId, courseName, reason });
  }

  requestUnenrollment(courseId: number, reason: string, studentId?: number, courseName?: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/request-unenrollment`, { courseId, studentId, courseName, reason });
  }
}