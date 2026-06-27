import {
    OverlayArrow,
    Tooltip as AriaTooltip,
    type TooltipProps as AriaTooltipProps,
    TooltipTrigger,
    type TooltipTriggerComponentProps,
} from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';

export type TooltipProps = Omit<AriaTooltipProps, 'children'> & {
    children: React.ReactNode;
};

/**
 * Hover/focus hint for a focusable control (ADR-0035). Use only on focusable
 * triggers (buttons, links) — RAC `Tooltip` needs a focusable trigger and is
 * not shown on touch, so truncation reveals keep `title=` instead.
 */
export function Tooltip({ children, ...props }: TooltipProps) {
    return (
        <AriaTooltip
            {...props}
            offset={6}
            className={composeTailwindRenderProps(
                props.className,
                'max-w-xs rounded-lg bg-fg-1 px-2.5 py-1.5 text-xs text-bg-0 shadow-lg outline-none ' +
                    'data-[entering]:opacity-0 data-[exiting]:opacity-0 transition-opacity duration-120',
            )}
        >
            <OverlayArrow>
                <svg width={8} height={8} viewBox="0 0 8 8" className="fill-fg-1">
                    <path d="M0 0 L4 4 L8 0" />
                </svg>
            </OverlayArrow>
            {children}
        </AriaTooltip>
    );
}

export function TooltipHint({
    hint,
    delay = 400,
    children,
}: {
    hint: React.ReactNode;
    delay?: number;
    children: TooltipTriggerComponentProps['children'];
}) {
    return (
        <TooltipTrigger delay={delay}>
            {children}
            <Tooltip>{hint}</Tooltip>
        </TooltipTrigger>
    );
}
