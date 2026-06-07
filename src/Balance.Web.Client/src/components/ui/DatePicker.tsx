import { CalendarDays } from 'lucide-react';
import {
    Button,
    Calendar,
    DatePicker as AriaDatePicker,
    type DatePickerProps as AriaDatePickerProps,
    type DateValue,
    Dialog,
    Group,
    type ValidationResult,
} from 'react-aria-components';
import { cx } from '../../lib/cx';
import { CalendarMonthGrid, CalendarPickerHeader, StyledDateInput } from './calendar-parts';
import { parseIsoDate } from './iso-date';
import { composeTailwindRenderProps } from './compose';
import { Description, FieldError, Label } from './field';
import { groupStyles } from './styles';
import { Popover } from './Popover';

export type DatePickerProps = Omit<
    AriaDatePickerProps<DateValue>,
    'value' | 'defaultValue' | 'onChange' | 'minValue' | 'maxValue' | 'children'
> & {
    /** ISO `yyyy-MM-dd`, or `''` when unset — matches the wire format (`DateOnly`). */
    value: string;
    onChange: (value: string) => void;
    minValue?: string;
    maxValue?: string;
    label?: string;
    description?: string;
    errorMessage?: string | ((validation: ValidationResult) => string);
    /** Extra classes for the field group (e.g. a toolbar width). */
    fieldClassName?: string;
};

/**
 * Segmented date field with a calendar popover. The app talks ISO strings;
 * `CalendarDate` stays inside this component (ADR-0024).
 */
export function DatePicker({
    value,
    onChange,
    minValue,
    maxValue,
    label,
    description,
    errorMessage,
    fieldClassName,
    ...props
}: DatePickerProps) {
    return (
        <AriaDatePicker
            {...props}
            value={parseIsoDate(value)}
            onChange={date => {
                onChange(date === null ? '' : date.toString());
            }}
            minValue={minValue === undefined ? undefined : parseIsoDate(minValue)}
            maxValue={maxValue === undefined ? undefined : parseIsoDate(maxValue)}
            className={composeTailwindRenderProps(props.className, 'group flex flex-col gap-1')}
        >
            {label !== undefined && <Label>{label}</Label>}
            <Group
                className={cx(
                    groupStyles,
                    'group-data-[invalid]:border-danger px-3',
                    fieldClassName,
                )}
            >
                <StyledDateInput className="flex flex-1 items-center" />
                <Button
                    aria-label="Open calendar"
                    className="flex items-center px-1 text-fg-3 outline-none cursor-pointer data-[hovered]:text-fg-1 data-[focus-visible]:text-fg-1 data-[disabled]:opacity-60"
                >
                    <CalendarDays size={15} strokeWidth={2} aria-hidden="true" />
                </Button>
            </Group>
            {description !== undefined && <Description>{description}</Description>}
            <FieldError>{errorMessage}</FieldError>
            <Popover placement="bottom start">
                <Dialog className="p-3 outline-none">
                    <Calendar>
                        <CalendarPickerHeader />
                        <CalendarMonthGrid />
                    </Calendar>
                </Dialog>
            </Popover>
        </AriaDatePicker>
    );
}
