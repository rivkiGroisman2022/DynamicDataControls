import { ChangeDetectionStrategy, Component, computed, input, model, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { OperatorInfo, SelectableField } from '../../../core/models/api.models';
import { ConditionGroup, ConditionNode, ConditionRule, isRule, Logic } from '../../../core/models/definition';
import { emptyGroup, emptyRule, removeAt, replaceAt, serverPath } from '../../../core/models/definition-tree';
import { ConditionRuleEditor } from './condition-rule';

/**
 * A group of conditions joined by "and" or "or". A recursive component: a group can contain groups,
 * so any logical tree can be built, e.g.  A and (B or C).
 */
@Component({
  selector: 'app-condition-group',
  imports: [ConditionRuleEditor, MatButtonModule, MatButtonToggleModule, MatIconModule, MatSlideToggleModule, MatTooltipModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './condition-group.html',
  styleUrl: './condition-group.scss',
})
export class ConditionGroupEditor {
  static readonly MaxDepth = 3;

  readonly group = model.required<ConditionGroup>();
  readonly fields = input.required<SelectableField[]>();
  readonly operators = input.required<OperatorInfo[]>();
  /** Position in the tree (indexes), for matching server errors to the right condition. */
  readonly path = input<number[]>([]);
  readonly depth = input(0);
  /** Errors by server path ("condition.conditions[0].value" → messages). */
  readonly errors = input<ReadonlyMap<string, string[]>>(new Map());
  readonly removable = input(false);
  readonly remove = output<void>();

  protected readonly canNest = computed(() => this.depth() < ConditionGroupEditor.MaxDepth);
  protected readonly logicWord = computed(() => (this.group().logic === 'or' ? 'או' : 'וגם'));
  protected readonly ownErrors = computed(() => this.errors().get(serverPath(this.path())) ?? []);

  protected readonly isRule = isRule;
  protected asRule = (node: ConditionNode) => node as ConditionRule;
  protected asGroup = (node: ConditionNode) => node as ConditionGroup;

  protected childPath(index: number): number[] {
    return [...this.path(), index];
  }

  /** The rule's own errors: its path exactly, or anything below it (.field / .value ...). */
  protected ruleErrors(index: number): string[] {
    const prefix = serverPath(this.childPath(index));
    const result: string[] = [];
    for (const [key, messages] of this.errors()) {
      if (key === prefix || key.startsWith(prefix + '.')) result.push(...messages);
    }
    return result;
  }

  protected setLogic(logic: Logic): void {
    this.group.update((g) => ({ ...g, logic }));
  }

  protected setNegate(negate: boolean): void {
    this.group.update((g) => ({ ...g, negate }));
  }

  protected addRule(): void {
    this.group.update((g) => ({ ...g, conditions: [...g.conditions, emptyRule()] }));
  }

  protected addGroup(): void {
    // A sub-group starts with the opposite logic - that is the common case ("and" with an "or" inside).
    this.group.update((g) => ({ ...g, conditions: [...g.conditions, emptyGroup(g.logic === 'and' ? 'or' : 'and')] }));
  }

  protected replaceChild(index: number, node: ConditionNode): void {
    this.group.update((g) => ({ ...g, conditions: replaceAt(g.conditions, index, node) }));
  }

  protected removeChild(index: number): void {
    this.group.update((g) => ({ ...g, conditions: removeAt(g.conditions, index) }));
  }
}
