import { ControlDefinition } from './definition';

// Interfaces mirroring the server's DTOs (DataControls.Core.*Dto).

export type FieldDataType = 'string' | 'number' | 'date' | 'boolean';
export type Severity = 'low' | 'medium' | 'high';
export type ValueArity = 'none' | 'single' | 'range' | 'list';

// ---------- Metadata ----------

export interface EntityInfo {
  key: string;
  displayName: string;
  displayNamePlural: string;
  description?: string | null;
  isControlTarget: boolean;
}

export interface FieldOption {
  value: string;
  label: string;
}

export interface SelectableField {
  path: string;
  displayName: string;
  groupLabel: string;
  description?: string | null;
  dataType: FieldDataType;
  unit?: string | null;
  depth: number;
  options: FieldOption[];
  operators: string[];
}

export interface OperatorInfo {
  key: string;
  label: string;
  dateLabel?: string | null;
  dataTypes: FieldDataType[];
  arity: ValueArity;
  supportsFieldComparison: boolean;
}

// ---------- Controls ----------

export interface ControlListItem {
  id: number;
  name: string;
  description?: string | null;
  entityKey: string;
  entityDisplayName: string;
  definitionText: string;
  severity: Severity;
  source: 'manual' | 'ai';
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
  lastRunAt?: string | null;
  lastRunStatus?: 'success' | 'failed' | null;
  lastViolationCount?: number | null;
}

export interface ControlDetails {
  id: number;
  name: string;
  description?: string | null;
  entityKey: string;
  entityDisplayName: string;
  definitionText: string;
  severity: Severity;
  source: 'manual' | 'ai';
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
  definition: ControlDefinition;
}

export interface ControlUpsertRequest {
  name: string;
  description?: string | null;
  severity: Severity;
  isActive: boolean;
  source?: 'manual' | 'ai';
  definition: ControlDefinition;
}

export interface ValidationError {
  path: string;
  message: string;
}

export interface DefinitionPreview {
  isValid: boolean;
  errors: ValidationError[];
  definitionText?: string | null;
  sql?: string | null;
}

// ---------- Run results ----------

export interface RunColumn {
  key: string;
  displayName: string;
  groupLabel: string;
  dataType: FieldDataType;
  unit?: string | null;
  isRuleField: boolean;
  options: FieldOption[];
}

export interface RunRule {
  index: number;
  text: string;
}

export interface ViolationRow {
  recordId: string;
  title: string;
  values: Record<string, unknown>;
  reasons: string[];
  matchedRules: number[];
}

export interface ControlRunResult {
  controlId?: number | null;
  controlName: string;
  entityKey: string;
  entityDisplayName: string;
  definitionText: string;
  totalViolations: number;
  returnedRows: number;
  isTruncated: boolean;
  durationMs: number;
  runAt: string;
  columns: RunColumn[];
  rules: RunRule[];
  rows: ViolationRow[];
}

export interface RunHistoryItem {
  runId: number;
  startedAt: string;
  durationMs?: number | null;
  status: 'success' | 'failed';
  violationCount?: number | null;
  errorMessage?: string | null;
}

export const SEVERITY_LABELS: Record<Severity, string> = {
  high: 'גבוהה',
  medium: 'בינונית',
  low: 'נמוכה',
};
