import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/** The app "shell": top bar + navigation + the area where screens are shown. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatToolbarModule, MatButtonModule, MatIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-toolbar class="topbar">
      <a class="brand" routerLink="/controls">
        <mat-icon>fact_check</mat-icon>
        <span>מערכת בקרות נתונים</span>
      </a>
      <span class="spacer"></span>
      <nav class="nav">
        <a mat-button routerLink="/controls" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">
          <mat-icon>list_alt</mat-icon><span class="label">הבקרות שלי</span>
        </a>
        <a mat-button routerLink="/controls/new" routerLinkActive="active">
          <mat-icon>add_circle_outline</mat-icon><span class="label">בקרה חדשה</span>
        </a>
        <a mat-button routerLink="/ai" routerLinkActive="active">
          <mat-icon>auto_awesome</mat-icon><span class="label">יצירה בשפה חופשית</span>
        </a>
      </nav>
    </mat-toolbar>
    <main>
      <router-outlet />
    </main>
  `,
  styles: `
    .topbar {
      position: sticky;
      top: 0;
      z-index: 10;
      background: var(--mat-sys-surface);
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      gap: 8px;
    }
    .brand {
      display: flex;
      align-items: center;
      gap: 8px;
      color: var(--mat-sys-primary);
      text-decoration: none;
      font-weight: 700;
      font-size: 18px;
    }
    .nav { display: flex; gap: 4px; }
    .nav a.active { background: var(--mat-sys-secondary-container); color: var(--mat-sys-on-secondary-container); }
    .nav mat-icon { margin-inline-end: 4px; }
    @media (max-width: 640px) {
      .brand span, .nav .label { display: none; }
    }
  `,
})
export class App {}
