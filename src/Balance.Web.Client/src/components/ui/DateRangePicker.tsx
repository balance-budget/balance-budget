import { useLingui } from '@lingui/react/macro';
import { CalendarDays } from 'lucide-react';
import {
    Button,
    DateRangePicker as AriaDateRangePicker,
    type DateRangePickerProps as AriaDateRangePickerProps,
    type DateValue,
    Dialog,
    Group,
    RangeCalendar,
    type ValidationResult,
} from 'react-aria-components';
import { cx } from '../../lib/cx';
import { CalendarMonthGrid, CalendarPickerHeader, StyledDateInput } from './calendar-parts';
import { parseIsoDate } from './iso-date';
import { composeTailwindRenderProps } from './compose';
import { Description, FieldError, Label } from './field';
import { groupStyles } from './styles';
import { Popover } from './Popover';

export type DateRange = {
    /** ISO `yyyy-MM-dd`, or `''` when unset. */
    from: string;
    to: string;
};

export type DateRangePickerProps = Omit<
    AriaDateRangePickerProps<DateValue>,
    'value' | 'defaultValue' | 'onChange' | 'minValue' | 'maxValue' | 'children'
> & {
    value: DateRange;
    onChange: (value: DateRange) => void;
    minValue?: string;
    maxValue?: string;
    label?: string;
    description?: string;
    errorMessage?: string | ((validation: ValidationResult) => string);
    /** Extra classes for the field group (e.g. a toolbar width). */
    fieldClassName?: string;
};

/**
 * Range field with a two-month calendar popover and month/year jump pickers.
 * The app talks `{ from, to }` ISO strings; `CalendarDate` stays inside this
 * component (ADR-0024). A range only propagates once both ends are set.
 */
export function DateRangePicker({
    value,
    onChange,
    minValue,
    maxValue,
    label,
    description,
    errorMessage,
    fieldClassName,
    ...props
}: DateRangePickerProps) {
    const { t } = useLingui();
    const start = parseIsoDate(value.from);
    const end = parseIsoDate(value.to);

    return (
        <AriaDateRangePicker
            {...props}
            value={start !== null && end !== null ? { start, end } : null}
            onChange={range => {
                onChange(
                    range === null
                        ? { from: '', to: '' }
                        : { from: range.start.toString(), to: range.end.toString() },
                );
            }}
            minValue={minValue === undefined ? undefined : parseIsoDate(minValue)}
            maxValue={maxValue === undefined ? undefined : parseIsoDate(maxValue)}
            className={composeTailwindRenderProps(props.className, 'group flex flex-col gap-1')}
        >
            {label !== undefined && <Label>{label}</Label>}
            <Group
                className={cx(
                    groupStyles,
                    'group-data-[invalid]:border-danger w-fit px-3',
                    fieldClassName,
                )}
            >
                <StyledDateInput slot="start" className="flex items-center" />
                <span aria-hidden="true" className="px-1 text-fg-3">
                    →
                </span>
                <StyledDateInput slot="end" className="flex items-center" />
                <Button
                    aria-label={t`Open calendar`}
                    className="flex items-center pl-2 pr-1 text-fg-3 outline-none cursor-pointer data-[hovered]:text-fg-1 data-[focus-visible]:text-fg-1 data-[disabled]:opacity-60"
                >
                    <CalendarDays size={15} strokeWidth={2} aria-hidden="true" />
                </Button>
            </Group>
            {description !== undefined && <Description>{description}</Description>}
            <FieldError>{errorMessage}</FieldError>
            <Popover placement="bottom start">
                <Dialog className="p-3 outline-none">
                    <RangeCalendar visibleDuration={{ months: 2 }}>
                        <CalendarPickerHeader />
                        <div className="flex items-start gap-6">
                            <CalendarMonthGrid />
                            <CalendarMonthGrid offset={{ months: 1 }} />
                        </div>
                    </RangeCalendar>
                </Dialog>
            </Popover>
        </AriaDateRangePicker>
    );
}
