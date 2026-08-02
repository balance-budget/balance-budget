import { Button, Link } from 'react-aria-components';
import { Trans, useLingui } from '@lingui/react/macro';
import type { ToOptions } from '@tanstack/react-router';
import { Icon, type IconName } from './Icon';
import { cx } from '../lib/cx';

type PaginationProps = {
    page: number;
    pageSize: number;
    totalCount: number;
    /** Where a page number points. Pages are links so they can be opened in a new
     *  tab and previewed like any other URL (ADR-0040). */
    hrefForPage: (page: number) => ToOptions;
};

export function Pagination({ page, pageSize, totalCount, hrefForPage }: PaginationProps) {
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
    const current = Math.min(page, totalPages);
    const hasPrev = current > 1;
    const hasNext = current < totalPages;
    const from = totalCount === 0 ? 0 : (current - 1) * pageSize + 1;
    const to = Math.min(current * pageSize, totalCount);
    const tokens = buildPageTokens(current, totalPages);

    const { t } = useLingui();
    return (
        <div className="flex items-center justify-between gap-3 pt-3">
            <span className="text-xs text-fg-3 tabular-nums">
                {totalCount === 0 ? (
                    '—'
                ) : (
                    <Trans>
                        {from}–{to} of {totalCount}
                    </Trans>
                )}
            </span>
            <div className="flex items-center gap-1">
                <PageButton
                    label={t`Previous page`}
                    icon="chevron-right"
                    rotated
                    href={hasPrev ? hrefForPage(current - 1) : undefined}
                />
                {tokens.map((token, i) =>
                    token === 'ellipsis' ? (
                        <span
                            key={`e${i}`}
                            className="px-1 text-xs text-fg-3 select-none"
                            aria-hidden="true"
                        >
                            …
                        </span>
                    ) : (
                        <PageNumber
                            key={token}
                            page={token}
                            current={current}
                            href={hrefForPage(token)}
                        />
                    ),
                )}
                <PageButton
                    label={t`Next page`}
                    icon="chevron-right"
                    href={hasNext ? hrefForPage(current + 1) : undefined}
                />
            </div>
        </div>
    );
}

type PageToken = number | 'ellipsis';

// Sliding window: first, last, and current ± 2. Insert ellipsis where there's a
// gap. When the total fits without ellipses (≤ 7 pages), render all of them
// directly to avoid a degenerate "1 [2] 3" with a useless ellipsis.
function buildPageTokens(current: number, totalPages: number): PageToken[] {
    if (totalPages <= 7) {
        return Array.from({ length: totalPages }, (_, i) => i + 1);
    }

    const pages = new Set<number>([
        1,
        totalPages,
        current - 2,
        current - 1,
        current,
        current + 1,
        current + 2,
    ]);
    const sorted = [...pages].filter(p => p >= 1 && p <= totalPages).sort((a, b) => a - b);
    const tokens: PageToken[] = [];
    let prev: number | null = null;
    for (const p of sorted) {
        if (prev !== null && p - prev > 1) {
            tokens.push('ellipsis');
        }
        tokens.push(p);
        prev = p;
    }
    return tokens;
}

// The current page is not a link: it goes nowhere, and an anchor to the page
// you're already on is noise for keyboard and screen-reader users.
function PageNumber({ page, current, href }: { page: number; current: number; href: ToOptions }) {
    const { t } = useLingui();
    const isCurrent = page === current;
    const Element = isCurrent ? Button : Link;
    return (
        <Element
            aria-label={t`Page ${page}`}
            aria-current={isCurrent ? 'page' : undefined}
            isDisabled={isCurrent}
            href={isCurrent ? undefined : href}
            className={cx(
                'min-w-[28px] px-2 py-1 rounded-lg text-xs tabular-nums text-center outline-none',
                'data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary',
                isCurrent
                    ? 'bg-surface-2 text-fg-1 font-medium cursor-default'
                    : 'text-fg-3 cursor-pointer data-[hovered]:text-fg-1 data-[hovered]:bg-surface-2',
            )}
        >
            {page}
        </Element>
    );
}

// Without a target page there is nothing to link to, so the edge states stay
// non-navigable rather than becoming href-less anchors.
function PageButton({
    label,
    icon,
    rotated,
    href,
}: {
    label: string;
    icon: IconName;
    rotated?: boolean;
    href: ToOptions | undefined;
}) {
    const disabled = href === undefined;
    const Element = disabled ? Button : Link;
    return (
        <Element
            aria-label={label}
            isDisabled={disabled}
            href={href}
            className={cx(
                'p-2 rounded-lg text-fg-3 outline-none cursor-pointer',
                'data-[hovered]:text-fg-1 data-[hovered]:bg-surface-2',
                'data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary',
                disabled && 'opacity-30 cursor-not-allowed',
            )}
        >
            <Icon
                name={icon}
                size={14}
                strokeWidth={2}
                className={rotated ? 'rotate-180' : undefined}
            />
        </Element>
    );
}
