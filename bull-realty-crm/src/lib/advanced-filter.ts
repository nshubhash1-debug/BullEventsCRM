/**
 * A Salesforce-style filter builder: each condition is a field, an operator and
 * a value, combined with either AND or OR. Operators are declared per field type
 * so the UI can offer only the ones that make sense, and evaluation is a pure
 * function over an accessor so the same engine works for any record type.
 */

export type FilterFieldType = "text" | "select" | "number" | "date";

export interface FilterFieldDef {
  id: string;
  label: string;
  type: FilterFieldType;
  options?: { label: string; value: string }[];
}

export interface FilterCondition {
  id: string;
  field: string;
  operator: string;
  value: string;
}

export type MatchMode = "all" | "any";

export interface OperatorDef {
  value: string;
  label: string;
  /** Operators like "is empty" take no value input. */
  unary?: boolean;
}

export const OPERATORS: Record<FilterFieldType, OperatorDef[]> = {
  text: [
    { value: "contains", label: "contains" },
    { value: "notContains", label: "does not contain" },
    { value: "equals", label: "equals" },
    { value: "notEquals", label: "does not equal" },
    { value: "startsWith", label: "starts with" },
    { value: "isEmpty", label: "is empty", unary: true },
    { value: "isNotEmpty", label: "is not empty", unary: true },
  ],
  select: [
    { value: "equals", label: "equals" },
    { value: "notEquals", label: "does not equal" },
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
  ],
  date: [
    { value: "onOrAfter", label: "on or after" },
    { value: "onOrBefore", label: "on or before" },
    { value: "lastNDays", label: "in the last N days" },
    { value: "olderThanNDays", label: "older than N days" },
  ],
};

export function defaultOperator(type: FilterFieldType) {
  return OPERATORS[type][0].value;
}

export function isUnary(type: FilterFieldType, operator: string) {
  return OPERATORS[type].find((op) => op.value === operator)?.unary === true;
}

export function newCondition(fields: FilterFieldDef[]): FilterCondition {
  const field = fields[0];
  return {
    id: `c-${Math.random().toString(36).slice(2, 9)}`,
    field: field.id,
    operator: defaultOperator(field.type),
    value: "",
  };
}

/** A condition only participates once it has everything it needs. */
export function isComplete(
  condition: FilterCondition,
  fields: FilterFieldDef[]
) {
  const field = fields.find((f) => f.id === condition.field);
  if (!field) return false;
  if (isUnary(field.type, condition.operator)) return true;
  return condition.value.trim() !== "";
}

function evaluateOne(
  raw: unknown,
  condition: FilterCondition,
  type: FilterFieldType
): boolean {
  const isBlank = raw === null || raw === undefined || raw === "";

  if (condition.operator === "isEmpty") return isBlank;
  if (condition.operator === "isNotEmpty") return !isBlank;

  const needle = condition.value.trim();

  if (type === "number") {
    const left = Number(raw);
    const right = Number(needle);
    if (Number.isNaN(left) || Number.isNaN(right)) return false;

    switch (condition.operator) {
      case "equals":
        return left === right;
      case "notEquals":
        return left !== right;
      case "greaterThan":
        return left > right;
      case "greaterOrEqual":
        return left >= right;
      case "lessThan":
        return left < right;
      case "lessOrEqual":
        return left <= right;
      default:
        return true;
    }
  }

  if (type === "date") {
    if (isBlank) return false;
    const when = new Date(String(raw)).getTime();
    if (Number.isNaN(when)) return false;

    if (condition.operator === "lastNDays" || condition.operator === "olderThanNDays") {
      const days = Number(needle);
      if (Number.isNaN(days)) return false;
      const cutoff = Date.now() - days * 86_400_000;
      return condition.operator === "lastNDays" ? when >= cutoff : when < cutoff;
    }

    const bound = new Date(needle).getTime();
    if (Number.isNaN(bound)) return false;
    return condition.operator === "onOrAfter" ? when >= bound : when <= bound;
  }

  // text and select
  const left = String(raw ?? "").toLowerCase();
  const right = needle.toLowerCase();

  switch (condition.operator) {
    case "contains":
      return left.includes(right);
    case "notContains":
      return !left.includes(right);
    case "equals":
      return left === right;
    case "notEquals":
      return left !== right;
    case "startsWith":
      return left.startsWith(right);
    default:
      return true;
  }
}

export function evaluateConditions<TRow>(
  row: TRow,
  conditions: FilterCondition[],
  mode: MatchMode,
  fields: FilterFieldDef[],
  getValue: (row: TRow, fieldId: string) => unknown
): boolean {
  const active = conditions.filter((c) => isComplete(c, fields));
  if (active.length === 0) return true;

  const results = active.map((condition) => {
    const field = fields.find((f) => f.id === condition.field);
    if (!field) return true;
    return evaluateOne(getValue(row, condition.field), condition, field.type);
  });

  return mode === "all" ? results.every(Boolean) : results.some(Boolean);
}

/** Human-readable chip text, e.g. `Stage equals Booked`. */
export function describeCondition(
  condition: FilterCondition,
  fields: FilterFieldDef[]
): string {
  const field = fields.find((f) => f.id === condition.field);
  if (!field) return "";

  const operator =
    OPERATORS[field.type].find((op) => op.value === condition.operator)?.label ??
    condition.operator;

  if (isUnary(field.type, condition.operator)) {
    return `${field.label} ${operator}`;
  }

  const label =
    field.options?.find((option) => option.value === condition.value)?.label ??
    condition.value;

  return `${field.label} ${operator} ${label}`;
}
