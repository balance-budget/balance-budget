/*
 * Shared field chrome — the single source of truth for how every Balance
 * input looks. Wrappers (TextField, Select, ComboBox, DatePicker, …) compose
 * these instead of repeating class strings, which is what keeps the inputs
 * visually consistent across the app (ADR-0024).
 *
 * Every control is exactly one size: h-9 with text-sm. Height is fixed (not
 * padding-derived) so text fields, date pickers, comboboxes and search boxes
 * align on a row by construction.
 */

const fieldChrome =
    'rounded-lg bg-surface-2 border border-border-soft text-fg-1 text-sm ' +
    'placeholder:text-fg-3 outline-none';

/** Chrome for a bare `<Input>` that is the whole field. */
export const inputStyles =
    `${fieldChrome} h-9 w-full px-3 ` +
    'focus:border-border-strong data-[invalid]:border-danger disabled:opacity-60';

/** Chrome for a `Group` that hosts inner inputs/buttons (number/date fields).
 *  Horizontal padding lives on the inner elements. */
export const groupStyles =
    `${fieldChrome} flex h-9 w-full items-center ` +
    'focus-within:border-border-strong data-[invalid]:border-danger data-[disabled]:opacity-60';
