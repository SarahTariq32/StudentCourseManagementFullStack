import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { PendingRequest } from '../models/admin.model';
import { Course } from '../models/course.model';
import { StudentProfile } from '../models/student.model';
import { ApiMessageResponse } from '../models/common.model';

export interface QueryParameters {
  pageIndex?: number;
  pageSize?: number;
  searchTerm?: string;
  sortBy?: string;
  isDescending?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getStudents(params?: QueryParameters): Observable<any> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.pageIndex) httpParams = httpParams.set('pageIndex', params.pageIndex);
      if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
      if (params.searchTerm) httpParams = httpParams.set('searchTerm', params.searchTerm);
      if (params.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
      if (params.isDescending !== undefined) httpParams = httpParams.set('isDescending', params.isDescending);
    }
    return this.http.get<any>(`${this.apiUrl}/Students`, { params: httpParams });
  }

  getStudentById(id: number): Observable<StudentProfile> {
    return this.http.get<StudentProfile>(`${this.apiUrl}/Students/${id}`);
  }

  updateStudent(id: number, data: { name: string; email: string; age: number }): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/Students/${id}`, data);
  }

  deleteStudent(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/Students/${id}`);
  }

  getCourses(params?: QueryParameters): Observable<any> {
    let httpParams = new HttpParams();
    if (params) {
      if (params.pageIndex) httpParams = httpParams.set('pageIndex', params.pageIndex);
      if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
      if (params.searchTerm) httpParams = httpParams.set('searchTerm', params.searchTerm);
      if (params.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
      if (params.isDescending !== undefined) httpParams = httpParams.set('isDescending', params.isDescending);
    }
    return this.http.get<any>(`${this.apiUrl}/Courses`, { params: httpParams });
  }

  getCourseById(id: number): Observable<Course> {
    return this.http.get<Course>(`${this.apiUrl}/Courses/${id}`);
  }

  createCourse(data: { name: string; credits: number }): Observable<Course> {
    return this.http.post<Course>(`${this.apiUrl}/Courses`, data);
  }

  updateCourse(id: number, data: { name: string; credits: number }): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/Courses/${id}`, data);
  }

  deleteCourse(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/Courses/${id}`);
  }

  getPendingRequests(): Observable<PendingRequest[]> {
    return this.http.get<PendingRequest[]>(`${this.apiUrl}/Courses/pending-requests`);
  }

  processRequest(requestId: number, approve: boolean): Observable<ApiMessageResponse> {
    return this.http.post<ApiMessageResponse>(`${this.apiUrl}/Courses/process-request`, { requestId, approve });
  }
}