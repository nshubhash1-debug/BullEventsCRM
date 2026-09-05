/**
 * The client half of the server's query engine.
 *
 * Filters are a tree, not a list: a node is either a leaf (field + operator +
 * value) or a group with a conjunction and children. That is what lets the
 * builder express `A AND (B OR C)` — a flat list can only ever say "all of" or
 * "any of", which runs out fast on a real pipeline.
 *
 * The shapes here mirror `Infrastructure/QueryEngine.cs` exactly. Field
 * definitions are not declared on this side at all — they are fetched from the
 * matching `/fields` endpoint, so the builder can only ever offer a field the
 * API is willing to filter on.
 */

export type FilterFieldType = "text" | "select" | "number" | "date" | "boolean";

export interface FilterOption {
  label: string;
  value: string;
}

export interface FilterField {
  id: string;
  label: string;
  type: FilterFieldType;
  options: FilterOption[] | null;
  /** Section heading in the field picker — "Requirement", "Attribution", … */
  group: string | null;
}

export interface FilterNode {
  /** Stable id for React keys and removal. Never sent to the server. */
  key: string;

  // ---- leaf ----
  field?: string;
  operator?: string;
  value?: string;
  value2?: string;
  values?: string[];

  // ---- group ----
  conjunction?: "and" | "or";
  children?: FilterNode[];

  negate?: boolean;
}

export interface SortSpec {
  field: string;
  descending: boolean;
}

export interface QueryRequest {
  search?: string;
  filter?: WireFilterNode | null;
  sort?: SortSpec[];
  page?: number;
  pageSize?: number;
  facets?: string[];
}

/** What actually goes on the wire — the same node without the client-only key. */
export interface WireFilterNode {
  field?: string;
  operator?: string;
  value?: string;
  value2?: string;
  values?: string[];
  conjunction?: string;
  children?: WireFilterNode[];
  negate?: boolean;
}

export interface FacetBucket {
  value: string;
  count: number;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  pageCount: number;
  facets: Record<string, FacetBucket[]>;
  aggregates: Record<string, number>;
}

/* ------------------------------------------------------------------ *
 * Operators
 * ------------------------------------------------------------------ */

export interface OperatorDef {
  value: string;
  label: string;
  /** Takes no value input — "is empty", "is overdue". */
  unary?: boolean;
  /** Takes two values — "between". */
  range?: boolean;
  /** Takes a multi-select — "is any of". */
  multi?: boolean;
}

export const OPERATORS: Record<FilterFieldType, OperatorDef[]> = {
  text: [
    { value: "contains", label: "contains" },
    { value: "notContains", label: "does not contain" },
    { value: "equals", label: "equals" },
    { value: "notEquals", label: "does not equal" },
    { value: "startsWith", label: "starts with" },
    { value: "endsWith", label: "ends with" },
    { value: "isEmpty", label: "is empty", unary: true },
    { value: "isNotEmpty", label: "is not empty", unary: true },
  ],
  select: [
    { value: "equals", label: "is" },
    { value: "notEquals", label: "is not" },
    { value: "in", label: "is any of", multi: true },
    { value: "notIn", label: "is none of", multi: true },
    { value: "isEmpty", label: "is empty", unary: true },
    { value: "isNotEmpty", label: "is not empty", unary: true },
  ],
  number: [
    { value: "equals", label: "=" },
    { value: "notEquals", label: "≠" },
    { value: "greaterThan", label: ">" },
    { value: "greaterOrEqual", label: "≥" },
    { value: "lessThan", label: "<" },
    { value: "lessOrEqual", label: "≤" },
    { value: "between", label: "between", range: true },
    { value: "isNull", label: "is blank", unary: true },
    { value: "isNotNull", label: "is not blank", unary: true },
  ],
  date: [
    { value: "today", label: "is today", unary: true },
    { value: "thisWeek", label: "this week", unary: true },
    { value: "thisMonth", label: "this month", unary: true },
    { value: "thisQuarter", label: "this quarter", unary: true },
    { value: "lastNDays", label: "in the last N days" },
    { value: "nextNDays", label: "in the next N days" },
    { value: "olderThanNDays", label: "older than N days" },
    { value: "overdue", label: "is in the past", unary: true },
    { value: "on", label: "on" },
    { value: "onOrAfter", label: "on or after" },
    { value: "onOrBefore", label: "on or before" },
    { value: "between", label: "between", range: true },
    { value: "isNull", label: "is blank", unary: true },
    { value: "isNotNull", label: "is not blank", unary: true },
  ],
  boolean: [
    { value: "isTrue", label: "is yes", unary: true },
    { value: "isFalse", label: "is no", unary: true },
  ],
};

export function operatorsFor(type: FilterFieldType) {
  return OPERATORS[type] ?? OPERATORS.text;
}

export function operatorDef(type: FilterFieldType, operator: string) {
  return operatorsFor(type).find((op) => op.value === operator);
}

export function defaultOperator(type: FilterFieldType) {
  return operatorsFor(type)[0].value;
}

/* ------------------------------------------------------------------ *
 * Tree construction and editing
 * ------------------------------------------------------------------ */

let sequence = 0;
function nextKey() {
  sequence += 1;
  return `n${sequence}`;
}

export function newLeaf(fields: FilterField[]): FilterNode {
  const field = fields[0];
  return {
    key: nextKey(),
    field: field?.id,
    operator: field ? defaultOperator(field.type) : "contains",
    value: "",
  };
}

export function newGroup(fields: FilterField[], conjunction: "and" | "or" = "or"): FilterNode {
  return {
    key: nextKey(),
    conjunction,
    children: [newLeaf(fields), newLeaf(fields)],
  };
}

export function emptyRoot(): FilterNode {
  return { key: nextKey(), conjunction: "and", children: [] };
}

export function isGroup(node: FilterNode) {
  return Array.isArray(node.children);
}

/** Structural edits return a new tree — the whole thing is treated as immutable state. */
export function updateNode(
  root: FilterNode,
  key: string,
  patch: Partial<FilterNode>
): FilterNode {
  if (root.key === key) return { ...root, ...patch };
  if (!root.children) return root;

  return {
    ...root,
    children: root.children.map((child) => updateNode(child, key, patch)),
  };
}

export function removeNode(root: FilterNode, key: string): FilterNode {
  if (!root.children) return root;

  return {
    ...root,
    children: root.children
      .filter((child) => child.key !== key)
      .map((child) => removeNode(child, key)),
  };
}

export function addToGroup(
  root: FilterNode,
  groupKey: string,
  node: FilterNode
): FilterNode {
  if (root.key === groupKey && root.children) {
    return { ...root, children: [...root.children, node] };
  }
  if (!root.children) return root;

  return {
    ...root,
    children: root.children.map((child) => addToGroup(child, groupKey, node)),
  };
}

/* ------------------------------------------------------------------ *
 * Validation and serialisation
 * ------------------------------------------------------------------ */

export function isComplete(node: FilterNode, fields: FilterField[]): boolean {
  if (isGroup(node)) {
    return (node.children ?? []).some((child) => isComplete(child, fields));
  }

  const field = fields.find((f) => f.id === node.field);
  if (!field || !node.operator) return false;

  const def = operatorDef(field.type, node.operator);
  if (!def) return false;
  if (def.unary) return true;
  if (def.multi) return (node.values ?? []).length > 0;
  if (def.range) return !!node.value?.trim() && !!node.value2?.trim();

  return !!node.value?.trim();
}

export function countConditions(node: FilterNode, fields: FilterField[]): number {
  if (isGroup(node)) {
    return (node.children ?? []).reduce(
      (sum, child) => sum + countConditions(child, fields),
      0
    );
  }
  return isComplete(node, fields) ? 1 : 0;
}

/**
 * Strips incomplete branches and the client-only keys. Returns null when
 * nothing survives, so the caller can omit the filter entirely rather than
 * sending an empty group the server has to reason about.
 */
export function toWire(
  node: FilterNode,
  fields: FilterField[]
): WireFilterNode | null {
  if (isGroup(node)) {
    const children = (node.children ?? [])
      .map((child) => toWire(child, fields))
      .filter((child): child is WireFilterNode => child !== null);

    if (children.length === 0) return null;

    return {
      conjunction: node.conjunction ?? "and",
      negate: node.negate,
      children,
    };
  }

  if (!isComplete(node, fields)) return null;

  const field = fields.find((f) => f.id === node.field)!;
  const def = operatorDef(field.type, node.operator!)!;

  return {
    field: node.field,
    operator: node.operator,
    value: def.unary || def.multi ? undefined : node.value,
    value2: def.range ? node.value2 : undefined,
    values: def.multi ? node.values : undefined,
    negate: node.negate,
  };
}

/** Chip text — `Stage is any of New, Contacted`. */
export function describe(node: FilterNode, fields: FilterField[]): string {
  const field = fields.find((f) => f.id === node.field);
  if (!field || !node.operator) return "";

  const def = operatorDef(field.type, node.operator);
  const operator = def?.label ?? node.operator;
  const label = (raw: string) =>
    field.options?.find((o) => o.value === raw)?.label ?? raw;

  if (def?.unary) return `${field.label} ${operator}`;
  if (def?.multi) {
    return `${field.label} ${operator} ${(node.values ?? []).map(label).join(", ")}`;
  }
  if (def?.range) return `${field.label} ${operator} ${node.value} – ${node.value2}`;

  return `${field.label} ${operator} ${label(node.value ?? "")}`;
}

/** Groups fields by their server-supplied section for the field picker. */
export function groupFields(fields: FilterField[]) {
  const groups = new Map<string, FilterField[]>();

  for (const field of fields) {
    const key = field.group ?? "Other";
    const bucket = groups.get(key);
    if (bucket) bucket.push(field);
    else groups.set(key, [field]);
  }

  return Array.from(groups, ([name, items]) => ({ name, items }));
}
