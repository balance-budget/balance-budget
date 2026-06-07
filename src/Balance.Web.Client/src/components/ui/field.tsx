import {
    FieldError as AriaFieldError,
    type FieldErrorProps,
    Group,
    type GroupProps,
    Input as AriaInput,
    type InputProps,
    Label as AriaLabel,
    type LabelProps,
    Text,
    type TextProps,
} from 'react-aria-components';
import { cx } from '../../lib/cx';
import { composeTailwindRenderProps } from './compose';

/*
 * Shared field chrome — the single source of truth for how every Balance
 * input looks. Wrappers (TextField, Select, ComboBox, DatePicker, …) compose
 * these instead of repeating class strings, which is what keeps the inputs
 * visually consistent across the app (ADR-0024).
 */

/** Chrome for a bare `<Input>` that is the whole field. */
export const inputStyles =
    'w-full px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 ' +
    'placeholder:text-fg-3 outline-none focus:border-border-strong ' +
    'data-[invalid]:border-danger disabled:opacity-60';

/** Chrome for a `Group` that hosts inner inputs/buttons (number/date fields). */
export const groupStyles =
    'flex items-center w-full rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-14 ' +
    'focus-within:border-border-strong data-[invalid]:border-danger data-[disabled]:opacity-60';

export function Label(props: LabelProps) {
    return (
        <AriaLabel {...props} className={cx('text-12 font-medium text-fg-2', props.className)} />
    );
}

export function Description(props: TextProps) {
    return (
        <Text {...props} slot="description" className={cx('text-11 text-fg-3', props.className)} />
    );
}

export function FieldError(props: FieldErrorProps) {
    return (
        <AriaFieldError
            {...props}
            className={composeTailwindRenderProps(props.className, 'text-12 text-danger')}
        />
    );
}

export function Input(props: InputProps) {
    return (
        <AriaInput
            {...props}
            className={composeTailwindRenderProps(props.className, inputStyles)}
        />
    );
}

export function FieldGroup(props: GroupProps) {
    return (
        <Group {...props} className={composeTailwindRenderProps(props.className, groupStyles)} />
    );
}
