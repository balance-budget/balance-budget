import {
    TextField as AriaTextField,
    type TextFieldProps as AriaTextFieldProps,
    type ValidationResult,
} from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';
import { Description, FieldError, Input, Label } from './field';
import type { FieldSize } from './styles';

export type TextFieldProps = AriaTextFieldProps & {
    label?: string;
    description?: string;
    placeholder?: string;
    errorMessage?: string | ((validation: ValidationResult) => string);
    fieldSize?: FieldSize;
    /** Extra classes for the inner `<input>` (e.g. `tabular` for codes). */
    inputClassName?: string;
};

export function TextField({
    label,
    description,
    placeholder,
    errorMessage,
    fieldSize,
    inputClassName,
    ...props
}: TextFieldProps) {
    return (
        <AriaTextField
            {...props}
            className={composeTailwindRenderProps(props.className, 'flex flex-col gap-1')}
        >
            {label !== undefined && <Label>{label}</Label>}
            <Input placeholder={placeholder} fieldSize={fieldSize} className={inputClassName} />
            {description !== undefined && <Description>{description}</Description>}
            <FieldError>{errorMessage}</FieldError>
        </AriaTextField>
    );
}
