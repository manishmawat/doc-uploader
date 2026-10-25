import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { HostAuthService } from '../auth/host-auth.service';
import { DocumentsService, RecycleBinItem } from '../documents/documents.service';
import { describeError, formatSize } from '../shared/errors';

@Component({
  selector: 'app-recycle-bin',
  imports: [DatePipe],
  templateUrl: './recycle-bin.html',
})
export class RecycleBin implements OnInit {
  private readonly documentsService = inject(DocumentsService);
  protected readonly auth = inject(HostAuthService);
  protected readonly formatSize = formatSize;

  protected readonly items = signal<RecycleBinItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly message = signal<string | null>(null);
  /** Name of the item awaiting "delete forever" confirmation. */
  protected readonly confirmingDelete = signal<string | null>(null);
  /** Name of the item with a restore or delete in progress. */
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
      this.items.set(await firstValueFrom(this.documentsService.listRecycleBin()));
    } catch (err) {
      this.error.set(describeError(err, 'Could not load the recycle bin.'));
    } finally {
      this.loading.set(false);
    }
  }

  protected async restore(item: RecycleBinItem): Promise<void> {
    await this.run(item.name, `Could not restore ${item.name}.`, async () => {
      await firstValueFrom(this.documentsService.restore(item.name));
      this.remove(item);
      this.message.set(`Restored ${item.name} to My Documents.`);
    });
  }

  protected async deleteForever(item: RecycleBinItem): Promise<void> {
    this.confirmingDelete.set(null);
    await this.run(item.name, `Could not delete ${item.name}.`, async () => {
      await firstValueFrom(this.documentsService.deleteForever(item.name));
      this.remove(item);
      this.message.set(`Permanently deleted ${item.name}.`);
    });
  }

  private remove(item: RecycleBinItem): void {
    this.items.update((items) => items.filter((i) => i.name !== item.name));
  }

  private async run(name: string, failure: string, action: () => Promise<void>): Promise<void> {
    this.busy.set(name);
    this.error.set(null);
    this.message.set(null);
    try {
      await action();
    } catch (err) {
      this.error.set(describeError(err, failure));
    } finally {
      this.busy.set(null);
    }
  }
}
