import { ChevronDown } from 'lucide-react';
import type { ReactNode } from 'react';
import {
    Button,
    Select as AriaSelect,
    type SelectProps as AriaSelectProps,
    SelectValue,
    type ValidationResult,
} from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';
import { Description, FieldError, Label } from './field';
import { groupStyles } from './styles';
import { DropdownItem, DropdownListBox, DropdownSection } from './ListBox';
import { Popover } from './Popover';

export type SelectProps<T extends object> = Omit<AriaSelectProps<T>, 'children'> & {
    label?: string;
    description?: string;
    errorMessage?: string | ((validation: ValidationResult) => string);
    items?: Iterable<T>;
    children: ReactNode | ((item: T) => ReactNode);
};

export function Select<T extends object>({
    label,
    description,
    errorMessage,
    items,
    children,
    ...props
}: SelectProps<T>) {
    return (
        <AriaSelect
            {...props}
            className={composeTailwindRenderProps(props.className, 'group flex flex-col gap-1')}
        >
            {label !== undefined && <Label>{label}</Label>}
            <Button
                className={
                    groupStyles +
                    ' justify-between gap-2 px-3 py-2 text-start outline-none cursor-pointer ' +
                    'group-data-[invalid]:border-danger data-[focus-visible]:border-border-strong'
                }
            >
                <SelectValue className="flex-1 truncate data-[placeholder]:text-fg-3" />
                <ChevronDown size={14} aria-hidden="true" className="shrink-0 text-fg-3" />
            </Button>
            {description !== undefined && <Description>{description}</Description>}
            <FieldError>{errorMessage}</FieldError>
            <Popover className="w-(--trigger-width)">
                <DropdownListBox items={items}>{children}</DropdownListBox>
            </Popover>
        </AriaSelect>
    );
}

export { DropdownItem as SelectItem, DropdownSection as SelectSection };
