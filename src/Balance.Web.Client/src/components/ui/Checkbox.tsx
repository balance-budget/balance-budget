import { Check } from 'lucide-react';
import {
    CheckboxButton,
    CheckboxField,
    type CheckboxFieldProps,
    composeRenderProps,
    type ValidationResult,
} from 'react-aria-components';
import { FieldError } from './field';

export type CheckboxProps = CheckboxFieldProps & {
    errorMessage?: string | ((validation: ValidationResult) => string);
};

/** Checkbox with a custom box — children render as the (rich) label. */
export function Checkbox({ errorMessage, ...props }: CheckboxProps) {
    return (
        <CheckboxField {...props} className="flex flex-col gap-1">
            <CheckboxButton className="group flex items-start gap-2 text-fg-1 cursor-pointer data-[disabled]:opacity-60 outline-none">
                {composeRenderProps(props.children, (children, { isSelected, isIndeterminate }) => (
                    <>
                        <span
                            aria-hidden="true"
                            className={
                                'mt-[3px] flex size-4 shrink-0 items-center justify-center rounded-xs border transition-colors ' +
                                'border-border-strong bg-surface-2 ' +
                                'group-data-[selected]:border-brand-primary group-data-[selected]:bg-brand-primary ' +
                                'group-data-[indeterminate]:border-brand-primary group-data-[indeterminate]:bg-brand-primary ' +
                                'group-data-[focus-visible]:ring-1 group-data-[focus-visible]:ring-brand-primary'
                            }
                        >
                            {isIndeterminate ? (
                                <span className="h-[2px] w-2 bg-white" />
                            ) : isSelected ? (
                                <Check size={12} strokeWidth={3} className="text-white" />
                            ) : null}
                        </span>
                        {children}
                    </>
                ))}
            </CheckboxButton>
            <FieldError>{errorMessage}</FieldError>
        </CheckboxField>
    );
}
