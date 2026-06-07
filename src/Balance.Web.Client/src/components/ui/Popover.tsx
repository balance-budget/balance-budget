import { Popover as AriaPopover, type PopoverProps } from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';

/** Floating-surface chrome shared by Select, ComboBox, DatePicker, and pickers. */
export function Popover(props: PopoverProps) {
    return (
        <AriaPopover
            offset={4}
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'rounded-lg bg-bg-1 border border-border-soft shadow-overlay text-sm ' +
                    'max-h-(--available-height) ' +
                    'data-[entering]:opacity-0 data-[exiting]:opacity-0 transition-opacity duration-120',
            )}
        />
    );
}
