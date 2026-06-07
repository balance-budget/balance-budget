/*
 * Shared field chrome — the single source of truth for how every Balance
 * input looks. Wrappers (TextField, Select, ComboBox, DatePicker, …) compose
 * these instead of repeating class strings, which is what keeps the inputs
 * visually consistent across the app (ADR-0024).
 */

export type FieldSize = 'md' | 'sm';

const inputBase =
    'w-full rounded-sm bg-surface-2 border border-border-soft text-fg-1 ' +
    'placeholder:text-fg-3 outline-none focus:border-border-strong ' +
    'data-[invalid]:border-danger disabled:opacity-60';

const INPUT_SIZE: Record<FieldSize, string> = {
    md: 'px-3 py-2 text-14',
    sm: 'px-2 py-1 text-13',
};

/** Chrome for a bare `<Input>` that is the whole field. */
export function inputStyles(size: FieldSize = 'md'): string {
    return `${inputBase} ${INPUT_SIZE[size]}`;
}

/** Chrome for a `Group` that hosts inner inputs/buttons (number/date fields). */
export const groupStyles =
    'flex items-center w-full rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 ' +
    'focus-within:border-border-strong data-[invalid]:border-danger data-[disabled]:opacity-60';
