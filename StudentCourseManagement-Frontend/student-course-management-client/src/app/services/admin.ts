import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { PendingRequest } from '../models/admin.model';
import { PagedResult, Course } from '../models/course.model';
import { StudentProfile } from '../models/student.model';

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getStudents(): Observable<PagedResult<StudentProfile>> {
    return this.http.get<PagedResult<StudentProfile>>(`${this.apiUrl}/Students?pageIndex=1&pageSize=100`);
  }

  getStudentById(id: number): Observable<StudentProfile> {
    return this.http.get<StudentProfile>(`${this.apiUrl}/Students/${id}`);
  }

  updateStudent(id: number, data: { name: string; email: string; age: number }): Observable<any> {
    return this.http.put(`${this.apiUrl}/Students/${id}`, data);
  }

  deleteStudent(id: number): Observable<any> {
    return this.http.delete(`${this.apiUrl}/Students/${id}`);
  }

  getCourses(): Observable<PagedResult<Course> | Course[]> {
    return this.http.get<PagedResult<Course> | Course[]>(`${this.apiUrl}/Courses`);
  }

  getCourseById(id: number): Observable<Course> {
    return this.http.get<Course>(`${this.apiUrl}/Courses/${id}`);
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

  getPendingRequests(): Observable<PendingRequest[]> {
    return this.http.get<PendingRequest[]>(`${this.apiUrl}/Courses/pending-requests`);
  }

  processRequest(requestId: number, approve: boolean): Observable<any> {
    return this.http.post(`${this.apiUrl}/Courses/process-request`, { requestId, approve });
  }
}