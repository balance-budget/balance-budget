import { ChevronDown, Plus } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Button, ComboBox as AriaComboBox, Input, type Key } from 'react-aria-components';
import { cx } from '../lib/cx';
import { FieldError } from './ui/field';
import { DropdownItem, DropdownListBox, DropdownSection } from './ui/ListBox';
import { Popover } from './ui/Popover';
import { groupStyles } from './ui/styles';
import { groupBuckets, matchesQuery, type ComboboxItem } from './combobox.state';

/** Sentinel option ids — never collide with item keys (which are entity ids). */
const NONE_KEY = '__none__';
const CREATE_KEY = '__create__';

export type ComboboxProps<T> = {
    items: readonly ComboboxItem<T>[];
    value: T | null;
    /** Selecting a real item. Always called with `item.value`. */
    onChange: (value: T) => void;
    /** Selecting the "── None …" sentinel. Required when `noneLabel` is set. */
    onClear?: () => void;
    /** Selecting the "+ Create '<typed>'" sentinel. Required when `createLabel`
     *  is set. The argument is the trimmed typed text. */
    onCreate?: (typed: string) => void;
    /** When set, renders a "── None …" sentinel as the first option. */
    noneLabel?: string;
    /** When set and no exact label match exists for the typed text, renders a
     *  "+ Create '<typed>'" sentinel as the last option. */
    createLabel?: (typed: string) => string;
    /** Optional preferred group order for grouped pickers. */
    groupOrder?: readonly string[];
    /** Optional human-readable labels per group (e.g. {Expense: 'Expenses'}).
     *  Defaults to the raw group key when missing. */
    groupLabels?: Record<string, string>;
    placeholder?: string;
    disabled?: boolean;
    /** Visible-only label used by screen readers. */
    ariaLabel?: string;
    /** Field name for React Aria Form `validationErrors`. */
    name?: string;
    /** Minimum width (px) for the open listbox, letting it grow wider than the
     *  trigger when options are long (e.g. deep account paths). Capped to the
     *  viewport by React Aria. */
    listboxMinWidth?: number;
};

/**
 * The shared typeahead picker, built on React Aria's ComboBox (ADR-0024).
 * Filtering happens here (against `searchText`) rather than in React Aria so
 * the option rows can render rich nodes while matching on more than what's
 * displayed; sentinel rows ("None", "+ Create") are plain options with
 * reserved ids.
 */
export function Combobox<T>({
    items,
    value,
    onChange,
    onClear,
    onCreate,
    noneLabel,
    createLabel,
    groupOrder,
    groupLabels,
    placeholder,
    disabled,
    ariaLabel,
    name,
    listboxMinWidth,
}: ComboboxProps<T>) {
    const selectedItem = useMemo(() => items.find(i => i.value === value) ?? null, [items, value]);

    // While open the input is a query box (starts blank, shows all options);
    // closed it displays the selection. This mirrors the previous Combobox.
    const [open, setOpen] = useState(false);
    const [typed, setTyped] = useState('');

    const filtered = useMemo(
        () => items.filter(item => matchesQuery(item.searchText ?? item.label, typed)),
        [items, typed],
    );
    const buckets = useMemo(() => groupBuckets(filtered, groupOrder), [filtered, groupOrder]);

    const trimmed = typed.trim();
    const createText =
        createLabel !== undefined &&
        trimmed.length > 0 &&
        !items.some(i => i.label.toLowerCase() === trimmed.toLowerCase())
            ? createLabel(trimmed)
            : null;

    function commit(key: Key | null) {
        if (key === null) return;
        if (key === NONE_KEY) {
            onClear?.();
        } else if (key === CREATE_KEY) {
            onCreate?.(trimmed);
        } else {
            const item = items.find(i => i.key === key);
            if (item) onChange(item.value);
        }
    }

    return (
        <AriaComboBox
            aria-label={ariaLabel}
            name={name}
            isDisabled={disabled}
            menuTrigger="focus"
            defaultFilter={() => true}
            value={selectedItem?.key ?? null}
            onChange={commit}
            inputValue={open ? typed : (selectedItem?.label ?? '')}
            onInputChange={v => {
                if (open) setTyped(v);
            }}
            onOpenChange={isOpen => {
                setOpen(isOpen);
                setTyped('');
            }}
            className="group flex flex-col gap-1"
        >
            <div className={cx(groupStyles, 'group-data-[invalid]:border-danger')}>
                <Input
                    placeholder={placeholder}
                    className="flex-1 min-w-0 px-3 py-2 bg-transparent outline-none text-13 placeholder:text-fg-3 disabled:opacity-60"
                />
                <Button className="px-2 self-stretch text-fg-3 outline-none cursor-pointer data-[hovered]:text-fg-1 data-[focus-visible]:text-fg-1 disabled:opacity-60">
                    <ChevronDown size={14} aria-hidden="true" />
                </Button>
            </div>
            <FieldError />
            <Popover
                className="max-w-(--available-width)"
                style={
                    listboxMinWidth === undefined
                        ? { minWidth: 'var(--trigger-width)' }
                        : { minWidth: `max(var(--trigger-width), ${listboxMinWidth.toString()}px)` }
                }
            >
                <DropdownListBox>
                    {noneLabel !== undefined && (
                        <DropdownItem
                            id={NONE_KEY}
                            textValue={noneLabel}
                            className="italic text-fg-3"
                        >
                            {noneLabel}
                        </DropdownItem>
                    )}
                    {buckets.map(bucket =>
                        bucket.group === undefined ? (
                            bucket.items.map(item => <OptionRow key={item.key} item={item} />)
                        ) : (
                            <DropdownSection
                                key={bucket.group}
                                id={bucket.group}
                                title={groupLabels?.[bucket.group] ?? bucket.group}
                            >
                                {bucket.items.map(item => (
                                    <OptionRow key={item.key} item={item} />
                                ))}
                            </DropdownSection>
                        ),
                    )}
                    {createText !== null && (
                        <DropdownItem
                            id={CREATE_KEY}
                            textValue={createText}
                            className="text-brand-primary"
                        >
                            <Plus size={12} strokeWidth={2} aria-hidden="true" />
                            <span className="truncate">{createText}</span>
                        </DropdownItem>
                    )}
                </DropdownListBox>
            </Popover>
        </AriaComboBox>
    );
}

function OptionRow<T>({ item }: { item: ComboboxItem<T> }) {
    return (
        <DropdownItem id={item.key} textValue={item.label}>
            <span className="truncate">{item.render ?? item.label}</span>
        </DropdownItem>
    );
}
