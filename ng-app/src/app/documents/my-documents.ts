import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { HostAuthService } from '../auth/host-auth.service';
import { DocumentItem, DocumentsService } from './documents.service';

@Component({
  selector: 'app-my-documents',
  imports: [DatePipe],
  templateUrl: './my-documents.html',
})
export class MyDocuments implements OnInit {
  private readonly documentsService = inject(DocumentsService);
  protected readonly auth = inject(HostAuthService);

  protected readonly documents = signal<DocumentItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Name of the document awaiting delete confirmation. */
  protected readonly confirmingDelete = signal<string | null>(null);
  /** Name of the document with a download or delete in progress. */
  protected readonly busy = signal<string | null>(null);

  ngOnInit(): void {
    if (this.auth.isFramed) {
      void this.refresh();
    }
  }

  protected async refresh(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.documents.set(await firstValueFrom(this.documentsService.list()));
    } catch (err) {
      this.error.set(describeError(err, 'Could not load your documents.'));
    } finally {
      this.loading.set(false);
    }
  }

  protected async download(doc: DocumentItem): Promise<void> {
    this.busy.set(doc.name);
    this.error.set(null);
    try {
      const { url } = await firstValueFrom(this.documentsService.getDownloadUrl(doc.name));
      // The SAS sets Content-Disposition: attachment, so the browser downloads instead of navigating
      const link = document.createElement('a');
      link.href = url;
      link.download = doc.name;
      link.click();
    } catch (err) {
      this.error.set(describeError(err, `Could not download ${doc.name}.`));
    } finally {
      this.busy.set(null);
    }
  }

  protected async delete(doc: DocumentItem): Promise<void> {
    this.confirmingDelete.set(null);
    this.busy.set(doc.name);
    this.error.set(null);
    try {
      await firstValueFrom(this.documentsService.delete(doc.name));
      this.documents.update((docs) => docs.filter((d) => d.name !== doc.name));
    } catch (err) {
      this.error.set(describeError(err, `Could not delete ${doc.name}.`));
    } finally {
      this.busy.set(null);
    }
  }

  protected formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}

/** Prefers the API's ErrorResponse message, then the error's own message. */
function describeError(err: unknown, fallback: string): string {
  if (err instanceof HttpErrorResponse) {
    // Status 0: network or CORS failure; err.error is then the browser's generic "Failed to fetch"
    if (err.status === 0) {
      return 'Could not reach the API. Check that it is running and allows this origin (CORS).';
    }
    return err.error?.message ?? fallback;
  }
  return err instanceof Error ? err.message : fallback;
}
