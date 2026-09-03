import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface PendingRequest {
  id: number;
  studentId: number;
  studentName: string;
  courseId: number;
  courseName: string;
  requestType: string; // 'Registration', 'Enroll', or 'Unenroll'
  reason: string;
}

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  // STUDENT CRUD
  getStudents(): Observable<any> {
    return this.http.get(`${this.apiUrl}/Students?pageIndex=1&pageSize=100`);
  }

  getStudentById(id: number): Observable<any> {
    return this.http.get(`${this.apiUrl}/Students/${id}`);
  }

  updateStudent(id: number, data: { name: string; email: string; age: number }): Observable<any> {
    return this.http.put(`${this.apiUrl}/Students/${id}`, data);
  }

  deleteStudent(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/Students/${id}`);
  }

  // COURSE CRUD (Search, Add, Update, Delete)
  getCourses(): Observable<any> {
    return this.http.get(`${this.apiUrl}/Courses`);
  }

  getCourseById(id: number): Observable<any> {
    return this.http.get(`${this.apiUrl}/Courses/${id}`);
  }

  createCourse(data: { name: string; credits: number }): Observable<any> {
    return this.http.post(`${this.apiUrl}/Courses`, data);
  }

  updateCourse(id: number, data: { name: string; credits: number }): Observable<any> {
    return this.http.put(`${this.apiUrl}/Courses/${id}`, data);
  }

  deleteCourse(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/Courses/${id}`);
  }

  // REQUEST MANAGEMENT
  getPendingRequests(): Observable<PendingRequest[]> {
    return this.http.get<PendingRequest[]>(`${this.apiUrl}/Courses/pending-requests`);
  }

  processRequest(requestId: number, approve: boolean): Observable<any> {
    return this.http.post(`${this.apiUrl}/Courses/process-request`, { requestId, approve });
  }
}