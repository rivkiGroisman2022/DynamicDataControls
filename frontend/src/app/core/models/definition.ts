/**
 * The control definition contract - identical to the C# record ControlDefinition on the server.
 * This is the single "language" shared by the UI, the server and the AI.
 */
export type JsonValue = string | number | boolean | null;
export type Logic = 'and' | 'or';

export interface ConditionGroup {
  type: 'group';
  logic: Logic;
  negate?: boolean;
  conditions: ConditionNode[];
}

export interface ConditionRule {
  type: 'rule';
  /** Business path: "salary" or "department.branch.isActive" */
  field: string;
  operator: string;
  value?: JsonValue;
  valueTo?: JsonValue;
  values?: JsonValue[];
  compareField?: string | null;
}

export type ConditionNode = ConditionGroup | ConditionRule;

export interface ControlDefinition {
  version: 1;
  entity: string;
  condition: ConditionNode;
}

export const isGroup = (node: ConditionNode): node is ConditionGroup => node.type === 'group';
export const isRule = (node: ConditionNode): node is ConditionRule => node.type === 'rule';
