import {
    Breadcrumb as AriaBreadcrumb,
    type BreadcrumbProps,
    Breadcrumbs as AriaBreadcrumbs,
    type BreadcrumbsProps,
    Link,
    type LinkProps,
} from 'react-aria-components';
import { cx } from '../../lib/cx';
import { composeTailwindRenderProps } from './compose';

export function Breadcrumbs<T extends object>(props: BreadcrumbsProps<T>) {
    return (
        <AriaBreadcrumbs
            {...props}
            className={cx('flex flex-wrap items-center gap-1 text-xs', props.className)}
        />
    );
}

// The separator's color is scoped to the same `:not(:first-child)` as its content:
// a bare `before:` utility emits `content: var(--tw-content)` (empty by default), and
// that empty box is a flex item the `gap-1` still spaces, indenting the first crumb
// away from the heading below it.
const CRUMB_CLASS =
    'flex items-center gap-1 min-w-0 ' +
    '[&:not(:first-child)]:before:content-["›"] [&:not(:first-child)]:before:text-fg-4';

/**
 * Styling for a crumb's clickable element, for crumbs that bring their own link.
 * Exported because the app wires no React Aria `RouterProvider`: a RAC `Link href`
 * would trigger a full page load, so a navigating crumb passes a router `Link` as a
 * child of {@link BreadcrumbItem} and styles it with this.
 */
export const breadcrumbLinkStyles =
    'truncate outline-none rounded-sm text-fg-3 cursor-pointer ' +
    'data-[hovered]:text-fg-1 data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary';

/** A crumb whose clickable element the caller supplies (a router `Link`). Contributes
 *  the `li` and the separator; the child owns navigation and its own styling. */
export function BreadcrumbItem({
    children,
    className,
}: {
    children: React.ReactNode;
    className?: string;
}) {
    return <AriaBreadcrumb className={cx(CRUMB_CLASS, className)}>{children}</AriaBreadcrumb>;
}

/** One crumb, navigated by RAC itself (`onPress`). Renders a separator before every
 *  crumb except the first. */
export function Breadcrumb(props: BreadcrumbProps & Omit<LinkProps, 'className' | 'style'>) {
    return (
        <AriaBreadcrumb className={composeTailwindRenderProps(props.className, CRUMB_CLASS)}>
            <Link
                {...props}
                className={
                    'outline-none rounded-sm data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary ' +
                    'data-[current]:text-fg-1 data-[current]:font-medium ' +
                    '[&:not([data-current])]:text-fg-3 [&:not([data-current])]:cursor-pointer ' +
                    '[&:not([data-current])]:data-[hovered]:text-fg-1'
                }
            />
        </AriaBreadcrumb>
    );
}
