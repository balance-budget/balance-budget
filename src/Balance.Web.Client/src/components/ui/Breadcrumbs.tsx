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
            className={cx('flex flex-wrap items-center gap-1 text-12', props.className)}
        />
    );
}

/** One crumb. Renders a separator before every crumb except the first. */
export function Breadcrumb(props: BreadcrumbProps & Omit<LinkProps, 'className' | 'style'>) {
    return (
        <AriaBreadcrumb
            className={composeTailwindRenderProps(
                props.className,
                'flex items-center gap-1 [&:not(:first-child)]:before:content-["›"] before:text-fg-4',
            )}
        >
            <Link
                {...props}
                className={
                    'outline-none rounded-xs data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary ' +
                    'data-[current]:text-fg-1 data-[current]:font-medium ' +
                    '[&:not([data-current])]:text-fg-3 [&:not([data-current])]:cursor-pointer ' +
                    '[&:not([data-current])]:data-[hovered]:text-fg-1'
                }
            />
        </AriaBreadcrumb>
    );
}
