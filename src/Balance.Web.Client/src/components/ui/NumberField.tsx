import {
    Input,
    NumberField as AriaNumberField,
    type NumberFieldProps as AriaNumberFieldProps,
    type ValidationResult,
} from 'react-aria-components';
import { cx } from '../../lib/cx';
import { composeTailwindRenderProps } from './compose';
import { Description, FieldError, Label } from './field';
import { inputStyles } from './styles';

export type NumberFieldProps = AriaNumberFieldProps & {
    label?: string;
    description?: string;
    placeholder?: string;
    errorMessage?: string | ((validation: ValidationResult) => string);
    /** Extra classes for the inner `<input>` (width, alignment). */
    inputClassName?: string;
};

/**
 * Numeric input without stepper buttons — Balance amounts are typed, not
 * nudged. Formatting (currency style, locale separators) comes from the
 * `formatOptions` prop, which also constrains what characters can be typed.
 */
export function NumberField({
    label,
    description,
    placeholder,
    errorMessage,
    inputClassName,
    ...props
}: NumberFieldProps) {
    return (
        <AriaNumberField
            {...props}
            className={composeTailwindRenderProps(props.className, 'flex flex-col gap-1')}
        >
            {label !== undefined && <Label>{label}</Label>}
            <Input
                placeholder={placeholder}
                className={cx(inputStyles, 'tabular', inputClassName)}
            />
            {description !== undefined && <Description>{description}</Description>}
            <FieldError>{errorMessage}</FieldError>
        </AriaNumberField>
    );
}
