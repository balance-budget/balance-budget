import { ChevronDown } from 'lucide-react';
import type { ReactNode } from 'react';
import {
    Button,
    ComboBox as AriaComboBox,
    type ComboBoxProps as AriaComboBoxProps,
    Input,
    type ValidationResult,
} from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';
import { Description, FieldError, groupStyles, Label } from './field';
import { DropdownItem, DropdownListBox, DropdownSection } from './ListBox';
import { Popover } from './Popover';

export type ComboBoxProps<T extends object> = Omit<AriaComboBoxProps<T>, 'children'> & {
    label?: string;
    description?: string;
    errorMessage?: string | ((validation: ValidationResult) => string);
    placeholder?: string;
    children: ReactNode | ((item: T) => ReactNode);
    /** Extra classes for the popover (e.g. a wider min-width for long options). */
    popoverClassName?: string;
};

export function ComboBox<T extends object>({
    label,
    description,
    errorMessage,
    placeholder,
    children,
    popoverClassName,
    ...props
}: ComboBoxProps<T>) {
    return (
        <AriaComboBox
            {...props}
            className={composeTailwindRenderProps(props.className, 'group flex flex-col gap-1')}
        >
            {label !== undefined && <Label>{label}</Label>}
            <div className={groupStyles + ' group-data-[invalid]:border-danger'}>
                <Input
                    placeholder={placeholder}
                    className="flex-1 min-w-0 px-3 py-2 bg-transparent outline-none text-13 placeholder:text-fg-3 disabled:opacity-60"
                />
                <Button className="px-2 self-stretch text-fg-3 outline-none cursor-pointer data-[hovered]:text-fg-1 data-[focus-visible]:text-fg-1 disabled:opacity-60">
                    <ChevronDown size={14} aria-hidden="true" />
                </Button>
            </div>
            {description !== undefined && <Description>{description}</Description>}
            <FieldError>{errorMessage}</FieldError>
            <Popover className={popoverClassName ?? 'w-(--trigger-width)'}>
                <DropdownListBox>{children}</DropdownListBox>
            </Popover>
        </AriaComboBox>
    );
}

export { DropdownItem as ComboBoxItem, DropdownSection as ComboBoxSection };
