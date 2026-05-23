import type { ReactNode } from 'react';

type PanelProps = {
    children: ReactNode;
    className?: string;
};

/** Frosted card: surface-1 with backdrop blur, hairline border, radius-md. */
export function Panel({ children, className }: PanelProps) {
    return (
        <section
            className={[
                'bg-surface-1 backdrop-blur-[20px] border border-border-soft rounded-md p-5 px-[22px]',
                className,
            ]
                .filter(Boolean)
                .join(' ')}
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
