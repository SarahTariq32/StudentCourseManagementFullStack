import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { Course } from '../models/course.model';

@Injectable({
  providedIn: 'root'
})
export class CourseService {
  private apiUrl = `${environment.apiUrl}/Courses`;

  constructor(private http: HttpClient) {}

  getCourses(): Observable<any> {
    return this.http.get<any>(this.apiUrl);
  }
}