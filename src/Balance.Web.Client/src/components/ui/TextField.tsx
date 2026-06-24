import {
    TextField as AriaTextField,
    type TextFieldProps as AriaTextFieldProps,
    TextArea,
    type ValidationResult,
} from 'react-aria-components';
import { cx } from '../../lib/cx';
import { composeTailwindRenderProps } from './compose';
import { Description, FieldError, Input, Label } from './field';
import { inputStyles } from './styles';

export type TextFieldProps = AriaTextFieldProps & {
    label?: string;
    description?: string;
    placeholder?: string;
    errorMessage?: string | ((validation: ValidationResult) => string);
    /** Extra classes for the inner `<input>` (e.g. `tabular-nums` for codes). */
    inputClassName?: string;
    /** Render a multi-line `<textarea>` instead of a single-line input. */
    multiline?: boolean;
    /** Visible rows for the multi-line variant (ignored when single-line). */
    rows?: number;
};

export function TextField({
    label,
    description,
    placeholder,
    errorMessage,
    inputClassName,
    multiline,
    rows,
    ...props
}: TextFieldProps) {
    return (
        <AriaTextField
            {...props}
            className={composeTailwindRenderProps(props.className, 'flex flex-col gap-1')}
        >
            {label !== undefined && <Label>{label}</Label>}
            {multiline ? (
                <TextArea
                    placeholder={placeholder}
                    rows={rows}
                    className={cx(inputStyles, 'h-auto resize-none py-2', inputClassName)}
                />
            ) : (
                <Input placeholder={placeholder} className={inputClassName} />
            )}
            {description !== undefined && <Description>{description}</Description>}
            <FieldError>{errorMessage}</FieldError>
        </AriaTextField>
    );
}
