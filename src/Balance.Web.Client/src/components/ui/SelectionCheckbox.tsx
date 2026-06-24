import { useRef } from 'react';
import { Check } from 'lucide-react';
import { CheckboxButton, CheckboxField } from 'react-aria-components';

export type SelectionCheckboxProps = {
    'aria-label': string;
    isSelected: boolean;
    /** Presentational mixed state — used by "select all" headers. */
    isIndeterminate?: boolean;
    isDisabled?: boolean;
    /** Tooltip shown on the box (e.g. why the row can't be selected). */
    title?: string;
    /**
     * Fires on toggle. `shiftKey` reflects the modifier held at press time so
     * callers can implement range selection; non-range callers ignore it.
     */
    onChange: (opts: { shiftKey: boolean }) => void;
};

const boxClass =
    'flex size-4 shrink-0 items-center justify-center rounded-sm border transition-colors ' +
    'border-border-strong bg-surface-2 ' +
    'group-data-[selected]:border-brand-primary group-data-[selected]:bg-brand-primary ' +
    'group-data-[indeterminate]:border-brand-primary group-data-[indeterminate]:bg-brand-primary ' +
    'group-data-[focus-visible]:ring-1 group-data-[focus-visible]:ring-brand-primary ' +
    'group-data-[disabled]:opacity-40';

/**
 * Compact checkbox for table selection cells (row select, select-all header).
 * Built on React Aria's CheckboxField so keyboard, focus, and ARIA state come
 * for free, while a capture-phase pointer/key handler records the shift
 * modifier for range selection — something a native `onChange` can't surface.
 */
export function SelectionCheckbox({
    isSelected,
    isIndeterminate,
    isDisabled,
    title,
    onChange,
    'aria-label': ariaLabel,
}: SelectionCheckboxProps) {
    const shiftRef = useRef(false);
    return (
        <CheckboxField
            aria-label={ariaLabel}
            isSelected={isSelected}
            isIndeterminate={isIndeterminate}
            isDisabled={isDisabled}
            onPointerDown={e => {
                shiftRef.current = e.shiftKey;
            }}
            onKeyDown={e => {
                shiftRef.current = e.shiftKey;
            }}
            onChange={() => {
                onChange({ shiftKey: shiftRef.current });
                shiftRef.current = false;
            }}
        >
            <CheckboxButton className="group flex items-center outline-none data-[disabled]:cursor-not-allowed cursor-pointer">
                {({ isSelected: selected, isIndeterminate: mixed }) => (
                    <span aria-hidden="true" title={title} className={boxClass}>
                        {mixed ? (
                            <span className="h-[2px] w-2 bg-white" />
                        ) : selected ? (
                            <Check size={12} strokeWidth={3} className="text-white" />
                        ) : null}
                    </span>
                )}
            </CheckboxButton>
        </CheckboxField>
    );
}
