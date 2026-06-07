import {
    composeRenderProps,
    RadioButton,
    RadioField,
    type RadioFieldProps,
    RadioGroup as AriaRadioGroup,
    type RadioGroupProps as AriaRadioGroupProps,
    type ValidationResult,
} from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';
import { Description, FieldError, Label } from './field';

export type RadioGroupProps = AriaRadioGroupProps & {
    label?: string;
    description?: string;
    errorMessage?: string | ((validation: ValidationResult) => string);
};

export function RadioGroup({
    label,
    description,
    errorMessage,
    children,
    ...props
}: RadioGroupProps) {
    return (
        <AriaRadioGroup
            {...props}
            className={composeTailwindRenderProps(props.className, 'flex flex-col gap-1')}
        >
            {renderProps => (
                <>
                    {label !== undefined && <Label>{label}</Label>}
                    <div
                        className={
                            renderProps.orientation === 'vertical'
                                ? 'flex flex-col gap-2'
                                : 'flex flex-wrap gap-4'
                        }
                    >
                        {typeof children === 'function' ? children(renderProps) : children}
                    </div>
                    {description !== undefined && <Description>{description}</Description>}
                    <FieldError>{errorMessage}</FieldError>
                </>
            )}
        </AriaRadioGroup>
    );
}

export function Radio(props: RadioFieldProps) {
    return (
        <RadioField {...props}>
            <RadioButton className="group flex items-center gap-2 text-sm text-fg-1 cursor-pointer data-[disabled]:opacity-60 outline-none">
                {composeRenderProps(props.children, children => (
                    <>
                        <span
                            aria-hidden="true"
                            className={
                                'flex size-4 shrink-0 items-center justify-center rounded-full border transition-colors ' +
                                'border-border-strong bg-surface-2 ' +
                                'group-data-[selected]:border-brand-primary ' +
                                'group-data-[focus-visible]:ring-1 group-data-[focus-visible]:ring-brand-primary'
                            }
                        >
                            <span className="size-2 rounded-full bg-brand-primary opacity-0 transition-opacity group-data-[selected]:opacity-100" />
                        </span>
                        {children}
                    </>
                ))}
            </RadioButton>
        </RadioField>
    );
}
