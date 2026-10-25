import { HttpErrorResponse } from '@angular/common/http';

/** Prefers the API's ErrorResponse message, then the error's own message. */
export function describeError(err: unknown, fallback: string): string {
  if (err instanceof HttpErrorResponse) {
    // Status 0: network or CORS failure; err.error is then the browser's generic "Failed to fetch"
    if (err.status === 0) {
      return 'Could not reach the API. Check that it is running and allows this origin (CORS).';
    }
    return err.error?.message ?? fallback;
  }
  return err instanceof Error ? err.message : fallback;
}

export function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
