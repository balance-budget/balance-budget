import { Amount } from './Amount';
import type { BudgetSummary } from '../lib/domain';

type BudgetBarProps = {
    budget: BudgetSummary;
};

/**
 * Three-state ladder per the design system: under = muted gray;
 * near-over (>= 80%) = soft red; over = bright red. Orange is reserved
 * for brand-positive moments, never warnings.
 */
export function BudgetBar({ budget }: BudgetBarProps) {
    const pct = budget.limitMinor === 0
        ? 0
        : Math.min(100, (budget.spentMinor / budget.limitMinor) * 100);

    const isOver = budget.spentMinor > budget.limitMinor;
    const isNear = !isOver && budget.spentMinor / budget.limitMinor >= 0.8;

    const fillColor = isOver
        ? 'var(--color-danger-strong)'
        : isNear
            ? 'var(--color-danger)'
            : 'var(--color-fg-3)';

    const tagBg = isOver
        ? 'var(--color-danger-soft)'
        : isNear
            ? 'var(--color-danger-soft)'
            : 'var(--color-surface-2)';
    const tagFg = isOver
        ? 'var(--color-danger-strong)'
        : isNear
            ? 'var(--color-danger)'
            : 'var(--color-fg-3)';

    const tagLabel = isOver
        ? `over by ${Math.round(pct - 100)}%`
        : `${Math.round(pct)}%`;

    return (
        <div className="py-[10px] flex flex-col gap-[6px]">
            <div className="flex items-center justify-between gap-3">
                <div className="flex items-center gap-2 min-w-0">
                    <span
                        className="w-2 h-2 rounded-full opacity-60 shrink-0"
                        style={{ background: budget.accentColor }}
                    />
                    <span className="text-14 text-fg-2 truncate">{budget.name}</span>
                </div>
                <div className="flex items-center gap-[10px] shrink-0">
                    <span
                        className="px-[8px] py-[2px] rounded-full text-[11px] font-medium tabular"
                        style={{ background: tagBg, color: tagFg }}
                    >
                        {tagLabel}
                    </span>
                    <span className="font-mono text-[11px] text-fg-3 tabular">
                        <Amount minor={budget.spentMinor} currencyCode={budget.currencyCode} size="inline" />
                        <span className="mx-[2px]">/</span>
                        <Amount minor={budget.limitMinor} currencyCode={budget.currencyCode} size="inline" />
                    </span>
                </div>
            </div>
            <div className="h-[6px] rounded-full bg-surface-2 overflow-hidden">
                <div
                    className="h-full rounded-full transition-[width] duration-slow"
                    style={{ width: `${pct}%`, background: fillColor }}
                />
            </div>
        </div>
    );
}
