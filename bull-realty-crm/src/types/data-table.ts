import type { LucideIcon } from "lucide-react";

export interface Option {
  label: string;
  value: string;
  icon?: LucideIcon;
  count?: number;
}

declare module "@tanstack/react-table" {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  interface ColumnMeta<TData, TValue> {
    label?: string;
    placeholder?: string;
    variant?: "text" | "select" | "multiSelect";
    options?: Option[];
  }
}
