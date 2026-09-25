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

  
  

    streamPendingRequestsSummary(
    token: string,
    onMeta: (meta: { total: number; categories: RequestCategoryCount[] }) => void,
    onChunk: (chunk: string) => void,
    onDone: (summary: EnrollmentRequestAiSummary) => void,
    onError: (err: any) => void
  ): void {
    const url = `${this.apiUrl}/enrollmentrequests/ai-summary/stream`;

    fetch(url, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Accept': 'text/event-stream'
      }
    }).then(async (response) => {
      if (!response.ok) {
        throw new Error(`Stream request failed: ${response.statusText}`);
      }

      const reader = response.body?.getReader();
      if (!reader) throw new Error('ReadableStream unavailable.');

      const decoder = new TextDecoder('utf-8');
      let buffer = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            try {
              const rawJson = line.substring(6).trim();
              if (!rawJson) continue;

              const outer = JSON.parse(rawJson);   // undoes controller's JsonSerializer.Serialize(chunk)
              const envelope = JSON.parse(outer);  // { type: 'meta' | 'chunk' | 'done', ... }

              if (envelope.type === 'meta') {
                onMeta({ total: envelope.total, categories: envelope.categories });
              } else if (envelope.type === 'chunk') {
                onChunk(envelope.text);
              } else if (envelope.type === 'done') {
                onDone(envelope.summary);
              }
            } catch (e) {
              console.warn('Failed to parse SSE payload:', line, e);
            }
          }
        }
      }
    }).catch((err) => {
      onError(err);
    });
  }
}



