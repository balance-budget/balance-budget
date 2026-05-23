import type { ReactNode } from 'react';
import { cx } from '../lib/cx';

type PanelPadding = 'sm' | 'md';

type PanelProps = {
    children: ReactNode;
    /** sm = 18px (kpi tiles), md = 20px/22px (default card layout). */
    padding?: PanelPadding;
    className?: string;
};

const PADDING_CLASS: Record<PanelPadding, string> = {
    sm: 'p-[18px]',
    md: 'py-5 px-[22px]',
};

/** Frosted card: surface-1 with backdrop blur, hairline border, radius-md. */
export function Panel({ children, padding = 'md', className }: PanelProps) {
    return (
        <section
            className={cx(
                'bg-surface-1 backdrop-blur-card border border-border-soft rounded-md',
                PADDING_CLASS[padding],
                className,
            )}
        >
            {children}
        </section>
    );
}

type SectionHeadProps = {
    title: ReactNode;
    subtitle?: ReactNode;
    action?: ReactNode;
};

export function SectionHead({ title, subtitle, action }: SectionHeadProps) {
    return (
        <div className="flex items-baseline justify-between gap-4 mb-[14px]">
            <div className="flex flex-col gap-[2px] min-w-0">
                <h2 className="text-16 font-semibold leading-snug">{title}</h2>
                {subtitle ? <span className="text-14 text-fg-3">{subtitle}</span> : null}
            </div>
            {action}
        </div>
    );
}
