import { Check } from 'lucide-react';
import { CheckboxButton, CheckboxField } from 'react-aria-components';

const boxClass =
    'flex size-4 shrink-0 items-center justify-center rounded-sm border transition-colors ' +
    'border-border-strong bg-surface-2 ' +
    'group-data-[selected]:border-brand-primary group-data-[selected]:bg-brand-primary ' +
    'group-data-[indeterminate]:border-brand-primary group-data-[indeterminate]:bg-brand-primary ' +
    'group-data-[focus-visible]:ring-1 group-data-[focus-visible]:ring-brand-primary ' +
    'group-data-[disabled]:opacity-40';

/**
 * Compact checkbox for RAC collection selection cells (row select and the
 * select-all header). Rendered with `slot="selection"` so RAC wires the
 * selection state, the indeterminate "some selected" header state, keyboard,
 * and ARIA labelling automatically (ADR-0035).
 */
export function CollectionSelectionCheckbox() {
    return (
        <CheckboxField slot="selection">
            <CheckboxButton className="group flex items-center outline-none data-[disabled]:cursor-not-allowed cursor-pointer">
                {({ isSelected, isIndeterminate }) => (
                    <span aria-hidden="true" className={boxClass}>
                        {isIndeterminate ? (
                            <span className="h-[2px] w-2 bg-white" />
                        ) : isSelected ? (
                            <Check size={12} strokeWidth={3} className="text-white" />
                        ) : null}
                    </span>
                )}
            </CheckboxButton>
        </CheckboxField>
    );
}
