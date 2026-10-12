import { Injectable, signal } from '@angular/core';
import { environment } from '../../environments/environment';

/** Message from the Blazor host page carrying an API access token. */
interface HostTokenMessage {
  type: 'token';
  accessToken: string;
  expiresOn: string;
}

interface HostTokenErrorMessage {
  type: 'token-error';
  message: string;
}

type HostMessage = HostTokenMessage | HostTokenErrorMessage;

/** Refresh the token when it has less than this left. */
const REFRESH_MARGIN_MS = 2 * 60 * 1000;
const REQUEST_TIMEOUT_MS = 15_000;

/**
 * Gets API access tokens from the Blazor app that hosts this app in an iframe.
 * Entra ID sign-in can't run inside an iframe, so the host signs in and hands over tokens via postMessage.
 */
@Injectable({ providedIn: 'root' })
export class HostAuthService {
  /** False when the app was opened directly instead of inside the Blazor host. */
  readonly isFramed = window.parent !== window;
  readonly error = signal<string | null>(null);

  private readonly allowedOrigins =
    environment.hostOrigins.length > 0 ? environment.hostOrigins : [window.location.origin];

  private token: { value: string; expiresOn: number } | null = null;
  private pending: Promise<string> | null = null;
  private hostOrigin: string | null = null;

  /** Returns a valid access token, asking the host for a new one when needed. */
  getToken(): Promise<string> {
    if (this.token && this.token.expiresOn - Date.now() > REFRESH_MARGIN_MS) {
      return Promise.resolve(this.token.value);
    }
    return (this.pending ??= this.requestToken().finally(() => (this.pending = null)));
  }

  private requestToken(): Promise<string> {
    if (!this.isFramed) {
      return Promise.reject(new Error('Open this page from the Document Uploader app.'));
    }

    return new Promise<string>((resolve, reject) => {
      const timeout = setTimeout(() => {
        window.removeEventListener('message', onMessage);
        reject(new Error('The host app did not provide an access token.'));
      }, REQUEST_TIMEOUT_MS);

      const onMessage = (event: MessageEvent<HostMessage>) => {
        if (event.source !== window.parent || !this.allowedOrigins.includes(event.origin)) {
          return;
        }
        const data = event.data;
        if (data?.type !== 'token' && data?.type !== 'token-error') {
          return;
        }

        clearTimeout(timeout);
        window.removeEventListener('message', onMessage);
        this.hostOrigin = event.origin;

        if (data.type === 'token-error') {
          this.error.set(data.message);
          reject(new Error(data.message));
          return;
        }

        this.error.set(null);
        this.token = { value: data.accessToken, expiresOn: Date.parse(data.expiresOn) };
        resolve(data.accessToken);
      };

      window.addEventListener('message', onMessage);

      // Before the first reply we don't know which allowed origin is hosting us, so ask each of them;
      // postMessage drops the message for any origin that doesn't match the parent.
      for (const origin of this.hostOrigin ? [this.hostOrigin] : this.allowedOrigins) {
        window.parent.postMessage({ type: 'token-request' }, origin);
      }
    });
  }
}
