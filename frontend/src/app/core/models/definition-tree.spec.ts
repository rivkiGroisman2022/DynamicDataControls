import { ConditionGroup, ConditionRule } from './definition';
import { asRootGroup, cleanDefinition, countRules, emptyGroup, removeAt, replaceAt, serverPath } from './definition-tree';

describe('definition-tree', () => {
  const rule = (field: string): ConditionRule => ({ type: 'rule', field, operator: 'isEmpty' });

  it('wraps a single-rule root in an AND group', () => {
    const root = asRootGroup(rule('email'));
    expect(root.type).toBe('group');
    expect(root.logic).toBe('and');
    expect(root.conditions).toHaveLength(1);
  });

  it('keeps an existing group root untouched', () => {
    const group = emptyGroup('or');
    expect(asRootGroup(group)).toBe(group);
  });

  it('replaceAt and removeAt return new arrays (immutability)', () => {
    const items = [1, 2, 3];
    const replaced = replaceAt(items, 1, 9);
    const removed = removeAt(items, 0);

    expect(replaced).toEqual([1, 9, 3]);
    expect(removed).toEqual([2, 3]);
    expect(items).toEqual([1, 2, 3]);
  });

  it('counts rules in nested groups', () => {
    const tree: ConditionGroup = {
      type: 'group', logic: 'and',
      conditions: [rule('a'), { type: 'group', logic: 'or', conditions: [rule('b'), rule('c')] }],
    };
    expect(countRules(tree)).toBe(3);
  });

  it('builds server error paths that match the backend validator', () => {
    expect(serverPath([])).toBe('condition');
    expect(serverPath([1, 0])).toBe('condition.conditions[1].conditions[0]');
  });

  it('cleans stale values that do not belong to the operator', () => {
    const cleaned = cleanDefinition({
      version: 1,
      entity: 'Employee',
      condition: {
        type: 'group', logic: 'and',
        conditions: [
          { type: 'rule', field: 'age', operator: 'lessThan', value: 18, valueTo: '', values: [] },
          { type: 'rule', field: 'endDate', operator: 'lessThan', value: 5, compareField: 'startDate' },
        ],
      },
    });

    const [first, second] = (cleaned.condition as ConditionGroup).conditions as ConditionRule[];
    expect(first).toEqual({ type: 'rule', field: 'age', operator: 'lessThan', value: 18 });
    expect(second).toEqual({ type: 'rule', field: 'endDate', operator: 'lessThan', compareField: 'startDate' });
  });
});
