import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Severity, SEVERITY_LABELS } from '../core/models/api.models';

/** Severity badge - a small reusable component. */
@Component({
  selector: 'app-severity-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="severity()">חומרה {{ label() }}</span>`,
  styles: `
    .badge {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 999px;
      font-size: 12px;
      font-weight: 500;
      white-space: nowrap;
    }
    .high { color: var(--severity-high); background: var(--severity-high-bg); }
    .medium { color: var(--severity-medium); background: var(--severity-medium-bg); }
    .low { color: var(--severity-low); background: var(--severity-low-bg); }
  `,
})
export class SeverityBadge {
  readonly severity = input.required<Severity>();
  protected readonly label = computed(() => SEVERITY_LABELS[this.severity()]);
}
