import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { HostAuthService } from '../auth/host-auth.service';
import { describeError, formatSize } from '../shared/errors';
import {
  DocumentItem,
  DocumentsService,
  DownloadUrlResponse,
  SHARE_EXPIRY_OPTIONS,
} from './documents.service';

@Component({
  selector: 'app-my-documents',
  imports: [DatePipe],
  templateUrl: './my-documents.html',
})
export class MyDocuments implements OnInit {
  private readonly documentsService = inject(DocumentsService);
  protected readonly auth = inject(HostAuthService);
  protected readonly formatSize = formatSize;
  protected readonly expiryOptions = SHARE_EXPIRY_OPTIONS;

  protected readonly documents = signal<DocumentItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Name of the document with a download, share or delete in progress. */
  protected readonly busy = signal<string | null>(null);
  /** Last moved-to-bin document, offered for Undo. */
  protected readonly lastDeleted = signal<string | null>(null);

  /** Name of the document whose share panel is open. */
  protected readonly sharing = signal<string | null>(null);
  protected readonly shareHours = signal<number>(24);
  protected readonly shareLink = signal<DownloadUrlResponse | null>(null);
  protected readonly copied = signal(false);

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
    await this.run(doc.name, `Could not download ${doc.name}.`, async () => {
      const { url } = await firstValueFrom(this.documentsService.getDownloadUrl(doc.name));
      // The SAS sets Content-Disposition: attachment, so the browser downloads instead of navigating
      const link = document.createElement('a');
      link.href = url;
      link.download = doc.name;
      link.click();
    });
  }

  protected toggleShare(doc: DocumentItem): void {
    this.sharing.set(this.sharing() === doc.name ? null : doc.name);
    this.shareLink.set(null);
    this.copied.set(false);
  }

  protected async createShareLink(doc: DocumentItem): Promise<void> {
    await this.run(doc.name, `Could not create a share link for ${doc.name}.`, async () => {
      this.shareLink.set(await firstValueFrom(this.documentsService.share(doc.name, this.shareHours())));
      this.copied.set(false);
    });
  }

  protected async copyShareLink(): Promise<void> {
    const link = this.shareLink();
    if (!link) return;
    try {
      await navigator.clipboard.writeText(link.url);
      this.copied.set(true);
    } catch {
      this.error.set('Could not copy automatically. Select the link and copy it.');
    }
  }

  protected async delete(doc: DocumentItem): Promise<void> {
    await this.run(doc.name, `Could not delete ${doc.name}.`, async () => {
      await firstValueFrom(this.documentsService.delete(doc.name));
      this.documents.update((docs) => docs.filter((d) => d.name !== doc.name));
      if (this.sharing() === doc.name) this.sharing.set(null);
      this.lastDeleted.set(doc.name);
    });
  }

  protected async undoDelete(): Promise<void> {
    const name = this.lastDeleted();
    if (!name) return;
    this.lastDeleted.set(null);
    await this.run(name, `Could not restore ${name}.`, async () => {
      await firstValueFrom(this.documentsService.restore(name));
      await this.refresh();
    });
  }

  /** Runs an action for one document with busy and error handling. */
  private async run(name: string, failure: string, action: () => Promise<void>): Promise<void> {
    this.busy.set(name);
    this.error.set(null);
    try {
      await action();
    } catch (err) {
      this.error.set(describeError(err, failure));
    } finally {
      this.busy.set(null);
    }
  }
}
