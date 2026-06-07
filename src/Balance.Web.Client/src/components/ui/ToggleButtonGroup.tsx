import {
    ToggleButton as AriaToggleButton,
    ToggleButtonGroup as AriaToggleButtonGroup,
    type ToggleButtonGroupProps,
    type ToggleButtonProps,
} from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';

/** Single-select pill group — used for date-range presets (MTD/QTD/…, 1M/3M/…). */
export function ToggleButtonGroup(props: ToggleButtonGroupProps) {
    return (
        <AriaToggleButtonGroup
            selectionMode="single"
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'flex flex-wrap items-center gap-[6px]',
            )}
        />
    );
}

export function ToggleButton(props: ToggleButtonProps) {
    return (
        <AriaToggleButton
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'px-[10px] py-[5px] rounded-full text-11 font-medium select-none cursor-pointer outline-none ' +
                    'text-fg-3 data-[hovered]:text-fg-1 ' +
                    'data-[selected]:bg-brand-primary-soft data-[selected]:text-brand-primary ' +
                    'data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary ' +
                    'data-[disabled]:opacity-60',
            )}
        />
    );
}
