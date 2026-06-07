import { ChevronDown, ChevronLeft, ChevronRight } from 'lucide-react';
import type { CalendarMonthPickerAria, CalendarYearPickerAria } from 'react-aria';
import {
    Button,
    CalendarCell,
    type CalendarCellRenderProps,
    CalendarGrid,
    CalendarGridBody,
    CalendarGridHeader,
    CalendarHeaderCell,
    CalendarMonthPicker,
    CalendarYearPicker,
    DateInput,
    type DateInputProps,
    DateSegment,
    Select,
    SelectValue,
} from 'react-aria-components';
import { cx } from '../../lib/cx';
import { DropdownItem, DropdownListBox } from './ListBox';
import { Popover } from './Popover';

/*
 * Shared internals for the DatePicker and DateRangePicker calendars: the
 * prev/next + month/year-picker header, day grids, and segment styling.
 */

/** Editable segmented date input (`<DateInput>` with Balance segment styling). */
export function StyledDateInput(props: Omit<DateInputProps, 'children'>) {
    return (
        <DateInput {...props}>
            {segment => (
                <DateSegment
                    segment={segment}
                    className={
                        'px-[2px] rounded-sm tabular-nums text-inherit outline-none caret-transparent ' +
                        'data-[placeholder]:text-fg-3 data-[disabled]:opacity-60 ' +
                        'data-[focused]:bg-brand-primary-soft data-[focused]:text-brand-primary ' +
                        'data-[invalid]:text-danger data-[invalid]:data-[focused]:text-brand-primary'
                    }
                />
            )}
        </DateInput>
    );
}

function headerSelect({ items, ...props }: CalendarMonthPickerAria | CalendarYearPickerAria) {
    return (
        <Select {...props} className="flex">
            <Button className="flex items-center gap-1 px-2 py-1 rounded-sm text-sm font-medium text-fg-1 outline-none cursor-pointer data-[hovered]:bg-surface-2 data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary">
                <SelectValue className="truncate" />
                <ChevronDown size={12} aria-hidden="true" className="text-fg-3" />
            </Button>
            <Popover>
                <DropdownListBox items={items}>
                    {(item: { id: number | string; formatted: string }) => (
                        <DropdownItem className="px-3 py-1.5">{item.formatted}</DropdownItem>
                    )}
                </DropdownListBox>
            </Popover>
        </Select>
    );
}

/** Prev/next month buttons around month + year dropdown pickers. */
export function CalendarPickerHeader() {
    return (
        <header className="flex items-center justify-between gap-1 pb-2">
            <Button
                slot="previous"
                aria-label="Previous month"
                className="flex items-center justify-center size-7 rounded-sm text-fg-3 outline-none cursor-pointer data-[hovered]:text-fg-1 data-[hovered]:bg-surface-2 data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary data-[disabled]:opacity-40"
            >
                <ChevronLeft size={15} aria-hidden="true" />
            </Button>
            <div className="flex items-center gap-1">
                <CalendarMonthPicker format="long">{headerSelect}</CalendarMonthPicker>
                <CalendarYearPicker>{headerSelect}</CalendarYearPicker>
            </div>
            <Button
                slot="next"
                aria-label="Next month"
                className="flex items-center justify-center size-7 rounded-sm text-fg-3 outline-none cursor-pointer data-[hovered]:text-fg-1 data-[hovered]:bg-surface-2 data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary data-[disabled]:opacity-40"
            >
                <ChevronRight size={15} aria-hidden="true" />
            </Button>
        </header>
    );
}

function cellClassName(renderProps: CalendarCellRenderProps): string {
    const {
        isSelected,
        isSelectionStart,
        isSelectionEnd,
        isOutsideMonth,
        isDisabled,
        isUnavailable,
    } = renderProps;
    const isEdge = isSelectionStart || isSelectionEnd;
    return cx(
        'flex size-8 items-center justify-center text-xs tabular-nums outline-none',
        isOutsideMonth && 'hidden',
        isDisabled || isUnavailable ? 'text-fg-4' : 'cursor-pointer',
        // Range middles get the soft wash; edges (and single selections) go solid.
        isSelected && !isEdge && 'bg-brand-primary-soft text-brand-primary',
        isEdge && 'bg-brand-primary text-white rounded-sm',
        !isSelected && !isDisabled && !isUnavailable && 'rounded-sm data-[hovered]:bg-surface-3',
        renderProps.isFocusVisible && 'ring-1 ring-brand-primary',
    );
}

/** One month of days. `offset` shifts it relative to the first visible month. */
export function CalendarMonthGrid({ offset }: { offset?: { months: number } }) {
    return (
        <CalendarGrid
            offset={offset}
            weekdayStyle="short"
            className="border-separate border-spacing-0"
        >
            <CalendarGridHeader>
                {day => (
                    <CalendarHeaderCell className="size-8 text-xs font-medium text-fg-3">
                        {day}
                    </CalendarHeaderCell>
                )}
            </CalendarGridHeader>
            <CalendarGridBody>
                {date => <CalendarCell date={date} className={cellClassName} />}
            </CalendarGridBody>
        </CalendarGrid>
    );
}
