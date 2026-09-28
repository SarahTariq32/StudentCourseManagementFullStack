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
    onError: (err: any) => void,
    signal?: AbortSignal
  ): void {
    const url = `${this.apiUrl}/enrollmentrequests/ai-summary/stream`;

    let finished = false;

    const handleLine = (line: string): void => {
      if (!line.startsWith('data: ')) return;

      const rawJson = line.substring(6).trim();
      if (!rawJson) return;

      let envelope: any;
      try {
        const outer = JSON.parse(rawJson);   // undoes controller's JsonSerializer.Serialize(chunk)
        envelope = JSON.parse(outer);        // { type: 'meta' | 'chunk' | 'done' | 'error', ... }
      } catch (e) {
        console.warn('Failed to parse SSE payload:', line, e);
        return;
      }

      if (envelope.type === 'meta') {
        onMeta({ total: envelope.total, categories: envelope.categories });
      } else if (envelope.type === 'chunk') {
        onChunk(envelope.text);
      } else if (envelope.type === 'done') {
        finished = true;
        onDone(envelope.summary);
      } else if (envelope.type === 'error') {
        finished = true;
        onError(new Error(envelope.message ?? 'Summary generation was interrupted.'));
      }
    };

    fetch(url, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Accept': 'text/event-stream'
      },
      signal
    }).then(async (response) => {
      if (!response.ok) {
        throw new Error(`Stream request failed: ${response.status} ${response.statusText}`);
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
          handleLine(line);

          if (finished) {
            await reader.cancel();   // stop reading once we have a final message
            return;
          }
        }
      }

      // The stream closed without a 'done' or 'error' message: it stopped midway
      if (!finished) {
        onError(new Error('The connection closed before the summary finished generating.'));
      }
    }).catch((err) => {
      if (!finished) {
        finished = true;
        onError(err);
      }
    });
  }
}



