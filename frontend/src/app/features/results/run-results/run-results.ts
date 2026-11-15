import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ControlRunResult, RunColumn, ViolationRow } from '../../../core/models/api.models';
import { FieldValuePipe, formatValue } from '../../../shared/value-format';

/**
 * Shows a run result: summary, filter by reason, free-text search, sorting, paging and CSV export.
 * A reusable component: the results screen, the editor's test run and the AI screen all use it.
 * All filtering happens on the client over the rows already returned (up to 1,000) - instant response with no extra server calls.
 */
@Component({
  selector: 'app-run-results',
  imports: [
    DatePipe, FieldValuePipe, MatTableModule, MatSortModule, MatPaginatorModule, MatFormFieldModule, MatInputModule,
    MatIconModule, MatChipsModule, MatButtonModule, MatSlideToggleModule, MatTooltipModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './run-results.html',
  styleUrl: './run-results.scss',
})
export class RunResults {
  readonly result = input.required<ControlRunResult>();
  /** Compact mode (e.g. inside the editor) - no summary header. */
  readonly compact = input(false);

  protected readonly search = signal('');
  protected readonly ruleFilter = signal<number[]>([]);
  protected readonly showAllColumns = signal(false);
  protected readonly sort = signal<Sort>({ active: '', direction: '' });
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(25);

  /** How many records failed on each condition - shown on the filter chip. */
  protected readonly ruleCounts = computed(() => {
    const { rules, rows } = this.result();
    return rules.map((rule) => ({ ...rule, count: rows.filter((r) => r.matchedRules.includes(rule.index)).length }));
  });

  /** By default: only the fields that appear in the control (focus on what matters). With the toggle: all fields. */
  protected readonly visibleColumns = computed<RunColumn[]>(() => {
    const columns = this.result().columns;
    if (this.showAllColumns()) return columns;
    const ruleColumns = columns.filter((c) => c.isRuleField);
    return ruleColumns.length ? ruleColumns : columns;
  });

  protected readonly displayedColumns = computed(() => ['title', ...this.visibleColumns().map((c) => c.key), 'reasons']);

  protected readonly filtered = computed<ViolationRow[]>(() => {
    const text = this.search().trim().toLowerCase();
    const selectedRules = this.ruleFilter();
    const columns = this.result().columns;

    return this.result().rows.filter((row) => {
      if (selectedRules.length && !row.matchedRules.some((i) => selectedRules.includes(i))) return false;
      if (!text) return true;
      return (
        row.title.toLowerCase().includes(text) ||
        row.reasons.some((r) => r.toLowerCase().includes(text)) ||
        columns.some((c) => formatValue(row.values[c.key], c).toLowerCase().includes(text))
      );
    });
  });

  protected readonly sorted = computed<ViolationRow[]>(() => {
    const { active, direction } = this.sort();
    const rows = this.filtered();
    if (!active || !direction) return rows;

    const factor = direction === 'asc' ? 1 : -1;
    const valueOf = (row: ViolationRow): unknown => (active === 'title' ? row.title : row.values[active]);
    return [...rows].sort((a, b) => factor * compareValues(valueOf(a), valueOf(b)));
  });

  protected readonly page = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.sorted().slice(start, start + this.pageSize());
  });

  protected setSearch(value: string): void {
    this.search.set(value);
    this.pageIndex.set(0);
  }

  protected setRuleFilter(indexes: number[]): void {
    this.ruleFilter.set(indexes ?? []);
    this.pageIndex.set(0);
  }

  protected onPage(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  /** CSV with a BOM so Excel opens Hebrew correctly. Exports the filtered rows (what the user sees). */
  protected exportCsv(): void {
    const columns = this.result().columns;
    const header = ['מזהה', 'רשומה', ...columns.map((c) => c.displayName), 'סיבת החריגה'];
    const lines = this.sorted().map((row) => [
      row.recordId,
      row.title,
      ...columns.map((c) => formatValue(row.values[c.key], c)),
      row.reasons.join(' | '),
    ]);
    const csv = [header, ...lines].map((line) => line.map(csvCell).join(',')).join('\r\n');

    const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.result().controlName} - ${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }
}

function compareValues(a: unknown, b: unknown): number {
  if (a === b) return 0;
  if (a === null || a === undefined) return 1;   // empty values always last
  if (b === null || b === undefined) return -1;
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  return String(a).localeCompare(String(b), 'he', { numeric: true });
}

function csvCell(value: string): string {
  return /[",\r\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
}
