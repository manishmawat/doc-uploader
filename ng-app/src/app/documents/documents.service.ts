import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface DocumentItem {
  name: string;
  sizeBytes: number;
  contentType: string | null;
  lastModified: string | null;
}

export interface DownloadUrlResponse {
  url: string;
  expiresAt: string;
}

/** Calls the backend's /api/Documents endpoints for the signed-in user's folder. */
@Injectable({ providedIn: 'root' })
export class DocumentsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/Documents`;

  list(): Observable<DocumentItem[]> {
    return this.http.get<DocumentItem[]>(this.baseUrl);
  }

  getDownloadUrl(name: string): Observable<DownloadUrlResponse> {
    return this.http.get<DownloadUrlResponse>(`${this.baseUrl}/${encodeURIComponent(name)}/download-url`);
  }

  delete(name: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(name)}`);
  }
}
