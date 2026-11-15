import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { ControlsApi } from '../../../core/api/controls-api';
import { toApiError } from '../../../core/api/api-error';
import { ControlListItem, Severity } from '../../../core/models/api.models';
import { Notifier } from '../../../core/notifier';
import { ConfirmDialog, ConfirmDialogData } from '../../../shared/confirm-dialog';
import { SeverityBadge } from '../../../shared/severity-badge';

/** Control list screen: search, filter, run, edit, duplicate and delete. */
@Component({
  selector: 'app-control-list',
  imports: [
    RouterLink, DatePipe, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatMenuModule, MatTooltipModule, MatProgressBarModule, SeverityBadge,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './control-list.html',
  styleUrl: './control-list.scss',
})
export class ControlList {
  private readonly api = inject(ControlsApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);
  private readonly router = inject(Router);

  // ----- State (signals) -----
  protected readonly controls = signal<ControlListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);

  protected readonly search = signal('');
  protected readonly entityFilter = signal<string>('');
  protected readonly severityFilter = signal<Severity | ''>('');

  /** The list of entities for the filter is derived from the controls themselves. */
  protected readonly entities = computed(() => {
    const map = new Map<string, string>();
    for (const c of this.controls()) map.set(c.entityKey, c.entityDisplayName);
    return [...map].map(([key, name]) => ({ key, name }));
  });

  /** Derived state: the filtered list is recomputed automatically whenever the list or a filter changes. */
  protected readonly filtered = computed(() => {
    const text = this.search().trim().toLowerCase();
    const entity = this.entityFilter();
    const severity = this.severityFilter();
    return this.controls().filter(
      (c) =>
        (!entity || c.entityKey === entity) &&
        (!severity || c.severity === severity) &&
        (!text || [c.name, c.description ?? '', c.definitionText].some((s) => s.toLowerCase().includes(text))),
    );
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.api.list().subscribe({
      next: (list) => {
        this.controls.set(list);
        this.loading.set(false);
      },
      error: (e) => {
        this.loadError.set(toApiError(e).userMessage);
        this.loading.set(false);
      },
    });
  }

  protected clearFilters(): void {
    this.search.set('');
    this.entityFilter.set('');
    this.severityFilter.set('');
  }

  protected run(control: ControlListItem): void {
    this.router.navigate(['/controls', control.id, 'results']);
  }

  /** Reuse: a new control that starts from a copy of an existing one. */
  protected duplicate(control: ControlListItem): void {
    this.router.navigate(['/controls/new'], { queryParams: { copyFrom: control.id } });
  }

  protected remove(control: ControlListItem): void {
    this.dialog
      .open<ConfirmDialog, ConfirmDialogData, boolean>(ConfirmDialog, {
        data: {
          title: 'מחיקת בקרה',
          message: `למחוק את הבקרה "${control.name}"? גם היסטוריית ההרצות שלה תימחק.`,
          confirmText: 'מחיקה',
          danger: true,
        },
        direction: 'rtl',
      })
      .afterClosed()
      .pipe(
        filter((confirmed) => confirmed === true),
        switchMap(() => this.api.delete(control.id)),
      )
      .subscribe({
        next: () => {
          this.controls.update((list) => list.filter((c) => c.id !== control.id));
          this.notifier.success('הבקרה נמחקה');
        },
        error: (e) => this.notifier.error(e),
      });
  }
}
