import {
    useEffect,
    useId,
    useLayoutEffect,
    useMemo,
    useRef,
    useState,
    type CSSProperties,
    type KeyboardEvent,
} from 'react';
import { createPortal } from 'react-dom';
import type { ReactNode } from 'react';

const LISTBOX_MAX_HEIGHT = 256; // matches the design's max-h-64
const LISTBOX_GAP = 4;
const LISTBOX_DOWN_MIN_SPACE = 160; // prefer popping up when below has less than this

/** Decide whether the listbox should drop down below the input or pop up above.
 *  Pops up when the space below the anchor is too small to comfortably show a
 *  useful slice of options *and* the space above is more generous — which is
 *  exactly the situation the BulkApplyFooter at the bottom of the viewport
 *  creates for its inline comboboxes. */
function listboxStyle(anchorRect: DOMRect): CSSProperties {
    const spaceBelow = window.innerHeight - anchorRect.bottom - LISTBOX_GAP;
    const spaceAbove = anchorRect.top - LISTBOX_GAP;
    const popUp = spaceBelow < LISTBOX_DOWN_MIN_SPACE && spaceAbove > spaceBelow;
    const maxHeight = Math.min(LISTBOX_MAX_HEIGHT, popUp ? spaceAbove : spaceBelow);
    if (popUp) {
        return {
            position: 'fixed',
            bottom: window.innerHeight - anchorRect.top + LISTBOX_GAP,
            left: anchorRect.left,
            width: anchorRect.width,
            maxHeight,
        };
    }
    return {
        position: 'fixed',
        top: anchorRect.bottom + LISTBOX_GAP,
        left: anchorRect.left,
        width: anchorRect.width,
        maxHeight,
    };
}
import { Icon } from './Icon';
import { cx } from '../lib/cx';
import {
    buildOptionList,
    nextActiveIndex,
    type ComboboxItem,
    type ComboboxOption,
} from './combobox.state';

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
};

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
}: ComboboxProps<T>) {
    const selectedItem = useMemo(() => items.find(i => i.value === value) ?? null, [items, value]);

    const [query, setQuery] = useState('');
    const [open, setOpen] = useState(false);
    const [active, setActive] = useState(-1);
    const wrapperRef = useRef<HTMLDivElement>(null);
    const inputRef = useRef<HTMLInputElement>(null);
    const listboxRef = useRef<HTMLUListElement>(null);
    const [anchorRect, setAnchorRect] = useState<DOMRect | null>(null);
    const listboxId = useId();
    const inputId = useId();

    const options = useMemo(
        () => buildOptionList({ items, query, noneLabel, createLabel, groupOrder }),
        [items, query, noneLabel, createLabel, groupOrder],
    );

    // Clamp at read-time rather than synchronising via an effect: when the
    // filter shrinks the option list, the previous `active` may dangle off
    // the end (or below zero), so resolve it here.
    const effectiveActive =
        options.length === 0 ? -1 : active >= 0 && active < options.length ? active : 0;

    // Capture the anchor rect synchronously before paint so the portalled
    // listbox renders at the correct position on first open, even if the
    // window has been scrolled since the previous open.
    useLayoutEffect(() => {
        if (!open) return;
        const el = inputRef.current;
        if (el) setAnchorRect(el.getBoundingClientRect());
    }, [open]);

    useEffect(() => {
        if (!open) return;
        function onDocClick(e: MouseEvent) {
            const target = e.target as Node;
            // The listbox is rendered outside `wrapperRef` (via a portal) to
            // escape the parent Panel's stacking context, so a click on it
            // would otherwise be treated as "outside" and close the popup
            // before the option's onMouseDown fires.
            if (wrapperRef.current?.contains(target)) return;
            if (listboxRef.current?.contains(target)) return;
            setOpen(false);
            setQuery('');
        }
        function updateRect() {
            const el = inputRef.current;
            if (el) setAnchorRect(el.getBoundingClientRect());
        }
        document.addEventListener('mousedown', onDocClick);
        window.addEventListener('resize', updateRect);
        window.addEventListener('scroll', updateRect, true);
        return () => {
            document.removeEventListener('mousedown', onDocClick);
            window.removeEventListener('resize', updateRect);
            window.removeEventListener('scroll', updateRect, true);
        };
    }, [open]);

    const displayLabel = selectedItem?.label ?? '';

    function commit(option: ComboboxOption<T>) {
        if (option.kind === 'item') {
            onChange(option.item.value);
        } else if (option.kind === 'none') {
            onClear?.();
        } else {
            onCreate?.(option.typed);
        }
        setOpen(false);
        setQuery('');
    }

    function handleKeyDown(e: KeyboardEvent<HTMLInputElement>) {
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            if (!open) setOpen(true);
            setActive(nextActiveIndex(effectiveActive, options.length, 1));
            return;
        }
        if (e.key === 'ArrowUp') {
            e.preventDefault();
            if (!open) setOpen(true);
            setActive(nextActiveIndex(effectiveActive, options.length, -1));
            return;
        }
        if (e.key === 'Enter') {
            const option = effectiveActive >= 0 ? options[effectiveActive] : null;
            if (option) {
                e.preventDefault();
                commit(option);
            }
            return;
        }
        if (e.key === 'Escape') {
            if (open) {
                e.preventDefault();
                setOpen(false);
                setQuery('');
            }
        }
    }

    return (
        <div ref={wrapperRef} className="relative">
            <input
                ref={inputRef}
                id={inputId}
                type="text"
                role="combobox"
                aria-expanded={open}
                aria-controls={listboxId}
                aria-autocomplete="list"
                aria-label={ariaLabel}
                value={open ? query : displayLabel}
                onChange={e => {
                    setQuery(e.target.value);
                    if (!open) setOpen(true);
                }}
                onFocus={() => {
                    setOpen(true);
                }}
                onKeyDown={handleKeyDown}
                disabled={disabled}
                placeholder={placeholder}
                autoComplete="off"
                className={cx(
                    'w-full px-3 py-2 rounded-sm bg-surface-2 border border-border-soft text-fg-1 text-[13px]',
                    'focus:outline-none focus:border-border-strong disabled:opacity-60',
                )}
            />
            {open &&
                options.length > 0 &&
                anchorRect &&
                createPortal(
                    <ul
                        ref={listboxRef}
                        id={listboxId}
                        role="listbox"
                        style={listboxStyle(anchorRect)}
                        className={cx(
                            'z-50 overflow-y-auto',
                            'rounded-sm bg-bg-1 border border-border-soft shadow-overlay text-[13px]',
                        )}
                    >
                        {renderOptions(options, effectiveActive, groupLabels, commit, setActive)}
                    </ul>,
                    document.body,
                )}
        </div>
    );
}

function renderOptions<T>(
    options: ComboboxOption<T>[],
    active: number,
    groupLabels: Record<string, string> | undefined,
    commit: (option: ComboboxOption<T>) => void,
    setActive: (i: number) => void,
) {
    const nodes: ReactNode[] = [];
    let lastGroup: string | undefined;
    options.forEach((option, i) => {
        if (option.kind === 'item' && option.group !== lastGroup) {
            lastGroup = option.group;
            if (option.group !== undefined) {
                nodes.push(
                    <li
                        key={`group-${option.group}`}
                        role="presentation"
                        className="px-3 py-1 text-[11px] text-fg-3 uppercase tracking-wider bg-surface-2 border-b border-border-soft"
                    >
                        {groupLabels?.[option.group] ?? option.group}
                    </li>,
                );
            }
        }
        const isActive = i === active;
        const key =
            option.kind === 'item'
                ? `item-${option.item.key}`
                : option.kind === 'none'
                  ? 'none'
                  : `create-${option.typed}`;
        nodes.push(
            <li
                key={key}
                role="option"
                aria-selected={isActive}
                onMouseDown={e => {
                    e.preventDefault();
                    commit(option);
                }}
                onMouseEnter={() => {
                    setActive(i);
                }}
                className={cx(
                    'px-3 py-2 cursor-pointer flex items-center gap-2',
                    isActive ? 'bg-brand-primary-soft text-brand-primary' : 'text-fg-1',
                    option.kind === 'create' && 'text-brand-primary',
                    option.kind === 'none' && 'text-fg-3 italic',
                )}
            >
                {option.kind === 'create' && <Icon name="plus" size={12} strokeWidth={2} />}
                <span className="truncate">
                    {option.kind === 'item' ? option.item.label : option.label}
                </span>
            </li>,
        );
    });
    return nodes;
}
