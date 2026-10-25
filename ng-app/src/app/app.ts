import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  selector: 'app-root',
  template: `
    <nav class="tabs" aria-label="My Documents sections">
      <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Files</a>
      <a routerLink="/recycle-bin" routerLinkActive="active">Recycle bin</a>
    </nav>
    <main><router-outlet /></main>
  `,
})
export class App {}
