import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { toApiError } from '../../../core/api/api-error';
import { ControlsApi } from '../../../core/api/controls-api';
import { ControlRunResult, RunHistoryItem } from '../../../core/models/api.models';
import { RunResults } from '../run-results/run-results';

/** Control results screen: runs the control as soon as it opens, shows results and run history. */
@Component({
  selector: 'app-control-results',
  imports: [RouterLink, DatePipe, MatButtonModule, MatIconModule, MatProgressBarModule, MatExpansionModule, RunResults],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <div>
          <h1>{{ result()?.controlName ?? 'הרצת בקרה' }}</h1>
          <p>תוצאות הבקרה: הרשומות החריגות והסיבה לכל אחת</p>
        </div>
        <div class="row">
          <a mat-button routerLink="/controls"><mat-icon>arrow_forward</mat-icon>לרשימה</a>
          <a mat-stroked-button [routerLink]="['/controls', id(), 'edit']"><mat-icon>edit</mat-icon>עריכה</a>
          <button mat-flat-button (click)="run()" [disabled]="running()"><mat-icon>refresh</mat-icon>הרצה מחדש</button>
        </div>
      </header>

      @if (running()) {
        <mat-progress-bar mode="indeterminate" />
        <p class="muted center">מריץ את הבקרה...</p>
      } @else if (error()) {
        <div class="state-box panel">
          <mat-icon>error_outline</mat-icon>
          <strong>הרצת הבקרה נכשלה</strong>
          <span>{{ error() }}</span>
          <button mat-stroked-button (click)="run()">ניסיון נוסף</button>
        </div>
      } @else if (result(); as r) {
        <section class="panel">
          <app-run-results [result]="r" />
        </section>
      }

      @if (history().length > 0) {
        <mat-expansion-panel class="history">
          <mat-expansion-panel-header>
            <mat-panel-title><mat-icon>history</mat-icon>&nbsp;היסטוריית הרצות</mat-panel-title>
          </mat-expansion-panel-header>
          <ul>
            @for (h of history(); track h.runId) {
              <li>
                <span>{{ h.startedAt | date: 'dd/MM/yyyy HH:mm:ss' }}</span>
                @if (h.status === 'success') {
                  <strong>{{ h.violationCount }} חריגות</strong>
                } @else {
                  <strong class="failed">נכשלה: {{ h.errorMessage }}</strong>
                }
                <span class="muted">{{ h.durationMs }} מ"ש</span>
              </li>
            }
          </ul>
        </mat-expansion-panel>
      }
    </div>
  `,
  styles: `
    .center { text-align: center; }
    .history { margin-top: 16px; }
    ul { margin: 0; padding: 0; list-style: none; }
    li { display: flex; flex-wrap: wrap; gap: 16px; padding: 6px 0; border-bottom: 1px solid var(--mat-sys-outline-variant); }
    .failed { color: var(--mat-sys-error); }
  `,
})
export class ControlResults implements OnInit {
  private readonly api = inject(ControlsApi);

  readonly id = input.required<string>();

  protected readonly result = signal<ControlRunResult | null>(null);
  protected readonly history = signal<RunHistoryItem[]>([]);
  protected readonly running = signal(false);
  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.run();
  }

  protected run(): void {
    this.running.set(true);
    this.error.set(null);
    this.api.run(Number(this.id())).subscribe({
      next: (result) => {
        this.result.set(result);
        this.running.set(false);
        this.loadHistory();
      },
      error: (e) => {
        this.error.set(toApiError(e).userMessage);
        this.running.set(false);
        this.loadHistory();
      },
    });
  }

  private loadHistory(): void {
    this.api.history(Number(this.id())).subscribe({ next: (h) => this.history.set(h), error: () => this.history.set([]) });
  }
}
