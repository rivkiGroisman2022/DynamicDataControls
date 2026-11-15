import { ConditionGroup, ConditionNode, ConditionRule, ControlDefinition, isGroup, Logic } from './definition';

/**
 * Pure functions for working with the condition tree.
 * Principle: Immutability - every change returns a new object instead of mutating the existing one.
 * That way Angular signals detect the change by reference, and the code is easy to test.
 */

export const emptyRule = (): ConditionRule => ({ type: 'rule', field: '', operator: '' });

export const emptyGroup = (logic: Logic = 'and'): ConditionGroup => ({
  type: 'group',
  logic,
  conditions: [emptyRule()],
});

/** The editor always works with a group at the root. A definition whose root is a single rule (e.g. from AI) is wrapped in a group. */
export function asRootGroup(node: ConditionNode | undefined | null): ConditionGroup {
  if (!node) return emptyGroup();
  return isGroup(node) ? node : { type: 'group', logic: 'and', conditions: [node] };
}

export function replaceAt<T>(items: readonly T[], index: number, item: T): T[] {
  return items.map((current, i) => (i === index ? item : current));
}

export function removeAt<T>(items: readonly T[], index: number): T[] {
  return items.filter((_, i) => i !== index);
}

export function countRules(node: ConditionNode): number {
  return isGroup(node) ? node.conditions.reduce((sum, child) => sum + countRules(child), 0) : 1;
}

/** Path in the server's format ("condition.conditions[1].conditions[0]") - lets server errors be shown next to the right condition. */
export function serverPath(indexPath: readonly number[]): string {
  return 'condition' + indexPath.map((i) => `.conditions[${i}]`).join('');
}

/**
 * Cleans the definition before sending it: removes empty properties that don't fit the operator.
 * E.g. a rule that was switched from "between" to "isEmpty" should not keep the old values.
 */
export function cleanDefinition(definition: ControlDefinition): ControlDefinition {
  return { ...definition, condition: cleanNode(definition.condition) };
}

function cleanNode(node: ConditionNode): ConditionNode {
  if (isGroup(node)) {
    const group: ConditionGroup = { type: 'group', logic: node.logic, conditions: node.conditions.map(cleanNode) };
    return node.negate ? { ...group, negate: true } : group;
  }
  const rule: ConditionRule = { type: 'rule', field: node.field, operator: node.operator };
  if (node.compareField) return { ...rule, compareField: node.compareField };
  if (node.value !== undefined && node.value !== null && node.value !== '') rule.value = node.value;
  if (node.valueTo !== undefined && node.valueTo !== null && node.valueTo !== '') rule.valueTo = node.valueTo;
  if (node.values?.length) rule.values = node.values;
  return rule;
}
