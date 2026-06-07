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
import { type FieldSize, groupStyles, inputStyles } from './styles';

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

export function Input({ fieldSize, ...props }: InputProps & { fieldSize?: FieldSize }) {
    return (
        <AriaInput
            {...props}
            className={composeTailwindRenderProps(props.className, inputStyles(fieldSize))}
        />
    );
}

export function FieldGroup(props: GroupProps) {
    return (
        <Group {...props} className={composeTailwindRenderProps(props.className, groupStyles)} />
    );
}
