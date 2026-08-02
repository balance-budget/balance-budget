import type { ReactNode } from 'react';
import { Link as AriaLink } from 'react-aria-components';
import type { ToOptions } from '@tanstack/react-router';
import { cx } from '../../lib/cx';

/**
 * The real anchor inside a navigating collection row, wrapping the content that
 * identifies the row (a table's row-header cell, a tree row's label).
 *
 * The row's own `href` already handles whole-row clicks and keyboard activation,
 * but React Aria renders rows as `role="row"` elements with a synthetic link, so
 * without this there is no anchor for the browser to right-click, preview in the
 * status bar, or open in a new tab (ADR-0040).
 *
 * Falls back to a plain wrapper when the row doesn't navigate, so callers can
 * share one row renderer between navigable and static collections.
 */
export function RowLink({
    href,
    className,
    children,
}: {
    href?: ToOptions;
    className?: string;
    children: ReactNode;
}) {
    if (!href) return <div className={className}>{children}</div>;
    return (
        <AriaLink
            href={href}
            className={cx(
                'text-inherit outline-none rounded-sm cursor-pointer',
                'data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary',
                className,
            )}
        >
            {children}
        </AriaLink>
    );
}
