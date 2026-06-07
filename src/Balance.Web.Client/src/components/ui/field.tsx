import {
    FieldError as AriaFieldError,
    type FieldErrorProps,
    Input as AriaInput,
    type InputProps,
    Label as AriaLabel,
    type LabelProps,
    Text,
    type TextProps,
} from 'react-aria-components';
import { cx } from '../../lib/cx';
import { composeTailwindRenderProps } from './compose';
import { inputStyles } from './styles';

export function Label(props: LabelProps) {
    return (
        <AriaLabel {...props} className={cx('text-xs font-medium text-fg-2', props.className)} />
    );
}

export function Description(props: TextProps) {
    return (
        <Text {...props} slot="description" className={cx('text-xs text-fg-3', props.className)} />
    );
}

export function FieldError(props: FieldErrorProps) {
    return (
        <AriaFieldError
            {...props}
            className={composeTailwindRenderProps(props.className, 'text-xs text-danger')}
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
