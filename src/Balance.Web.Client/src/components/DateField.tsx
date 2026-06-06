import { useRef, useState } from 'react';
import { cx } from '../lib/cx';
import { isValidIsoDate } from '../lib/dates';
import { Icon } from './Icon';

/**
 * A date input that always reads and writes `yyyy-MM-dd`, regardless of the
 * browser's locale. A native `<input type="date">` can't be reformatted by the
 * page (it follows the OS/browser locale, e.g. US `MM/DD/YYYY`), so this renders
 * an ISO-formatted text box plus a calendar button that opens the native picker
 * via `showPicker()` on a hidden date input. The value contract matches the old
 * native inputs: a `yyyy-MM-dd` string, or '' when unset.
 */
export type DateFieldProps = {
    value: string;
    onChange: (value: string) => void;
    min?: string;
    max?: string;
    required?: boolean;
    disabled?: boolean;
    ariaLabel?: string;
    id?: string;
    /** Classes for the visible text input (width, font size, border accents). */
    className?: string;
    /** Classes for the positioning wrapper (typically a width in toolbars). */
    wrapperClassName?: string;
};

export function DateField({
    value,
    onChange,
    min,
    max,
    required,
    disabled,
    ariaLabel,
    id,
    className,
    wrapperClassName,
}: DateFieldProps) {
    const [text, setText] = useState(value);
    const [focused, setFocused] = useState(false);
    const [lastValue, setLastValue] = useState(value);
    const nativeRef = useRef<HTMLInputElement>(null);

    // Mirror the controlled value into the text box whenever it changes from the
    // outside and the user isn't mid-edit — covers URL-backed filters, preset
    // buttons, and form resets. This is the "adjust state during render" pattern
    // (no effect): https://react.dev/learn/you-might-not-need-an-effect
    if (value !== lastValue) {
        setLastValue(value);
        if (!focused) setText(value);
    }

    function inRange(v: string): boolean {
        // ISO strings sort chronologically, so plain string comparison works.
        if (min !== undefined && min !== '' && v < min) return false;
        if (max !== undefined && max !== '' && v > max) return false;
        return true;
    }

    function isAcceptable(v: string): boolean {
        return v === '' || (isValidIsoDate(v) && inRange(v));
    }

    function commit(next: string) {
        setText(next);
        // Only propagate complete, in-range dates (or a clear); half-typed text
        // is held locally until it becomes valid.
        if (isAcceptable(next)) onChange(next);
    }

    function handleBlur() {
        setFocused(false);
        // Snap back to the committed value, discarding half-typed or out-of-range text.
        if (!isAcceptable(text)) setText(value);
    }

    function openPicker() {
        try {
            nativeRef.current?.showPicker();
        } catch {
            // showPicker is unsupported or blocked — the text box still works.
        }
    }

    return (
        <div className={cx('relative', wrapperClassName)}>
            <input
                type="text"
                inputMode="numeric"
                id={id}
                value={text}
                onChange={e => {
                    commit(e.target.value);
                }}
                onFocus={() => {
                    setFocused(true);
                }}
                onBlur={handleBlur}
                placeholder="yyyy-mm-dd"
                required={required}
                disabled={disabled}
                aria-label={ariaLabel}
                autoComplete="off"
                spellCheck={false}
                className={cx(className, 'w-full pr-9 tabular')}
            />
            <button
                type="button"
                onClick={openPicker}
                disabled={disabled}
                tabIndex={-1}
                aria-label="Open calendar"
                className="absolute inset-y-0 right-0 flex items-center px-2 text-fg-3 hover:text-fg-1 disabled:opacity-60"
            >
                <Icon name="calendar" size={15} strokeWidth={2} />
            </button>
            {/* Hidden native picker — supplies the calendar UI and anchors its
             *  popup to the field. pointer-events-none lets clicks reach the
             *  button beneath it. */}
            <input
                ref={nativeRef}
                type="date"
                tabIndex={-1}
                aria-hidden
                value={value}
                min={min}
                max={max}
                disabled={disabled}
                onChange={e => {
                    commit(e.target.value);
                }}
                className="pointer-events-none absolute inset-0 opacity-0"
            />
        </div>
    );
}
