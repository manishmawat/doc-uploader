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

export interface RecycleBinItem {
  name: string;
  sizeBytes: number;
  deletedOn: string | null;
  purgeOn: string | null;
}

/** Share link lifetimes the API accepts (BlobUpload:ShareLinkAllowedHours). */
export const SHARE_EXPIRY_OPTIONS = [
  { hours: 1, label: '1 hour' },
  { hours: 24, label: '1 day' },
  { hours: 72, label: '3 days' },
] as const;

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

  share(name: string, expiresInHours: number): Observable<DownloadUrlResponse> {
    return this.http.post<DownloadUrlResponse>(`${this.baseUrl}/${encodeURIComponent(name)}/share`, {
      expiresInHours,
    });
  }

  /** Moves the document to the recycle bin. */
  delete(name: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${encodeURIComponent(name)}`);
  }

  listRecycleBin(): Observable<RecycleBinItem[]> {
    return this.http.get<RecycleBinItem[]>(`${this.baseUrl}/recycle-bin`);
  }

  restore(name: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/recycle-bin/${encodeURIComponent(name)}/restore`, null);
  }

  deleteForever(name: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/recycle-bin/${encodeURIComponent(name)}`);
  }
}
