import { useLingui } from '@lingui/react/macro';
import { toCalendarDateTime } from '@internationalized/date';
import { CalendarClock } from 'lucide-react';
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
import { isoInstantFromLocal, parseIsoInstant } from './iso-date';
import { composeTailwindRenderProps } from './compose';
import { Description, FieldError, Label } from './field';
import { groupStyles } from './styles';
import { Popover } from './Popover';

export type DateTimePickerProps = Omit<
    AriaDatePickerProps<DateValue>,
    'value' | 'defaultValue' | 'onChange' | 'minValue' | 'maxValue' | 'children' | 'granularity'
> & {
    /** UTC instant (ISO 8601), or `''` when unset — matches the wire format. */
    value: string;
    onChange: (value: string) => void;
    label?: string;
    description?: string;
    errorMessage?: string | ((validation: ValidationResult) => string);
    /** Extra classes for the field group (e.g. a toolbar width). */
    fieldClassName?: string;
};

/**
 * Segmented date + time field with a calendar popover, the counterpart of
 * {@link DatePicker} for instant-typed fields. The app talks UTC instant
 * strings; the local-wall-clock `CalendarDateTime` stays inside this component
 * (ADR-0024).
 */
export function DateTimePicker({
    value,
    onChange,
    label,
    description,
    errorMessage,
    fieldClassName,
    ...props
}: DateTimePickerProps) {
    const { t } = useLingui();
    return (
        <AriaDatePicker
            {...props}
            granularity="minute"
            value={parseIsoInstant(value)}
            onChange={date => {
                onChange(date === null ? '' : isoInstantFromLocal(toCalendarDateTime(date)));
            }}
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
                    aria-label={t`Open calendar`}
                    className="flex items-center px-1 text-fg-3 outline-none cursor-pointer data-[hovered]:text-fg-1 data-[focus-visible]:text-fg-1 data-[disabled]:opacity-60"
                >
                    <CalendarClock size={15} strokeWidth={2} aria-hidden="true" />
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
