import { ChangeDetectionStrategy, Component, computed, input, model, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipInputEvent, MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { OperatorInfo, SelectableField } from '../../../core/models/api.models';
import { ConditionRule, JsonValue } from '../../../core/models/definition';

interface FieldGroup {
  label: string;
  fields: SelectableField[];
}

/**
 * A single condition: [field] [operator] [value].
 * Fully metadata-driven: which fields are shown, which operators fit each field type,
 * and which value input appears (number / date / list / comparison to another field) - nothing is hard-coded.
 */
@Component({
  selector: 'app-condition-rule',
  imports: [MatFormFieldModule, MatSelectModule, MatInputModule, MatButtonModule, MatIconModule, MatTooltipModule, MatChipsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './condition-rule.html',
  styleUrl: './condition-rule.scss',
})
export class ConditionRuleEditor {
  /** model() = two-way binding: the parent passes the rule in and gets back a new rule on every change. */
  readonly rule = model.required<ConditionRule>();
  readonly fields = input.required<SelectableField[]>();
  readonly operators = input.required<OperatorInfo[]>();
  readonly errors = input<string[]>([]);
  readonly removable = input(true);
  readonly remove = output<void>();

  protected readonly field = computed(() => this.fields().find((f) => f.path === this.rule().field));

  /** Fields grouped by entity ("עובד", "עובד › מחלקה"...) - shows the relations in business language. */
  protected readonly fieldGroups = computed<FieldGroup[]>(() => {
    const groups = new Map<string, SelectableField[]>();
    for (const f of this.fields()) groups.set(f.groupLabel, [...(groups.get(f.groupLabel) ?? []), f]);
    return [...groups].map(([label, fields]) => ({ label, fields }));
  });

  protected readonly availableOperators = computed(() => {
    const f = this.field();
    return f ? this.operators().filter((o) => f.operators.includes(o.key)) : [];
  });

  protected readonly operator = computed(() => this.operators().find((o) => o.key === this.rule().operator));
  protected readonly arity = computed(() => this.operator()?.arity ?? 'none');
  protected readonly compareMode = computed(() => !!this.rule().compareField);
  protected readonly hasOptions = computed(() => (this.field()?.options.length ?? 0) > 0);

  protected readonly compareCandidates = computed(() => {
    const f = this.field();
    return f ? this.fields().filter((x) => x.dataType === f.dataType && x.path !== f.path) : [];
  });

  protected readonly canCompare = computed(
    () => !!this.operator()?.supportsFieldComparison && this.compareCandidates().length > 0,
  );

  protected readonly inputType = computed(() => {
    switch (this.field()?.dataType) {
      case 'number': return 'number';
      case 'date': return 'date';
      default: return 'text';
    }
  });

  protected opLabel(op: OperatorInfo): string {
    return this.field()?.dataType === 'date' && op.dateLabel ? op.dateLabel : op.label;
  }

  protected fieldLabel(path: string | null | undefined): string {
    const f = this.fields().find((x) => x.path === path);
    return f ? `${f.displayName} (${f.groupLabel})` : '';
  }

  protected optionLabel(value: JsonValue): string {
    return this.field()?.options.find((o) => o.value === value)?.label ?? String(value);
  }

  // ----- Changes: each one creates a new rule (immutable) and resets what no longer fits -----

  protected setField(path: string): void {
    const f = this.fields().find((x) => x.path === path);
    const current = this.rule().operator;
    const operator = f?.operators.includes(current) ? current : (f?.operators[0] ?? '');
    this.rule.set({ type: 'rule', field: path, operator });
  }

  protected setOperator(key: string): void {
    const previous = this.operator();
    const next = this.operators().find((o) => o.key === key);
    const r = this.rule();
    const base: ConditionRule = { type: 'rule', field: r.field, operator: key };

    // Keep the value when moving between two operators of the same kind (e.g. "greater than" → "less than").
    if (next?.arity === previous?.arity) {
      if (r.compareField && next?.supportsFieldComparison) base.compareField = r.compareField;
      else {
        if (r.value !== undefined) base.value = r.value;
        if (r.valueTo !== undefined) base.valueTo = r.valueTo;
        if (r.values) base.values = r.values;
      }
    }
    this.rule.set(base);
  }

  protected setValue(raw: string | JsonValue, which: 'value' | 'valueTo' = 'value'): void {
    this.rule.update((r) => ({ ...r, [which]: this.parse(raw) }));
  }

  protected setValues(values: JsonValue[]): void {
    this.rule.update((r) => ({ ...r, values }));
  }

  protected addListValue(event: MatChipInputEvent): void {
    const text = event.value.trim();
    if (text) this.setValues([...(this.rule().values ?? []), this.parse(text)]);
    event.chipInput.clear();
  }

  protected removeListValue(index: number): void {
    this.setValues((this.rule().values ?? []).filter((_, i) => i !== index));
  }

  protected toggleCompare(): void {
    const r = this.rule();
    this.rule.set(
      this.compareMode()
        ? { type: 'rule', field: r.field, operator: r.operator }
        : { type: 'rule', field: r.field, operator: r.operator, compareField: this.compareCandidates()[0]?.path },
    );
  }

  protected setCompareField(path: string): void {
    this.rule.update((r) => ({ ...r, compareField: path }));
  }

  /** A number field gets a number (not a string) so the server receives a JSON value of the right type. */
  private parse(raw: string | JsonValue): JsonValue {
    if (raw === '' || raw === null || raw === undefined) return null;
    if (this.field()?.dataType === 'number' && typeof raw === 'string') {
      const n = Number(raw);
      return Number.isNaN(n) ? raw : n;
    }
    return raw;
  }
}
