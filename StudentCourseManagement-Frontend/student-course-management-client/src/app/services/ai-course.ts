import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';

export interface RecommendedCourse {
  id: number;
  name: string;
  reason: string;
}

export interface CourseRecommendationResponse {
  matchedCourses: RecommendedCourse[];
  advisorNote: string;
}

export interface RequestCategoryCount {
  category: string;
  count: number;
}

export interface EnrollmentRequestAiSummary {
  totalPendingRequests: number;
  categories: RequestCategoryCount[];
  summaryNote: string;
  isAiGenerated?: boolean;
  aiStatusMessage?: string;
  retryAfterSeconds?: number;
}

@Injectable({
  providedIn: 'root'
})
export class AiCourseService {
  private apiUrl = `${environment.apiUrl}/AiCourse`;

  constructor(private http: HttpClient) {}

  searchStrict(query: string): Observable<CourseRecommendationResponse> {
    const params = new HttpParams().set('query', query);
    return this.http.get<CourseRecommendationResponse>(`${this.apiUrl}/search/strict`, { params });
  }

  searchFreeform(query: string): Observable<CourseRecommendationResponse> {
    const params = new HttpParams().set('query', query);
    return this.http.get<CourseRecommendationResponse>(`${this.apiUrl}/search/free`, { params });
  }

  
  getPendingRequestsSummary(): Observable<EnrollmentRequestAiSummary> {
    return this.http.get<EnrollmentRequestAiSummary>(`${this.apiUrl}/enrollmentrequests/ai-summary`);
  }
}



