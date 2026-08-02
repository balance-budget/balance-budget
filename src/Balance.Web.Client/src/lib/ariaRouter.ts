import type { AnyRouter, NavigateOptions, ToOptions } from '@tanstack/react-router';

/**
 * Adapts a TanStack router to React Aria's `RouterProvider`, so every RAC `href`
 * renders a real anchor whose clicks navigate client-side (ADR-0040).
 *
 * `href` is a `ToOptions` object rather than a URL string (see the `RouterConfig`
 * augmentation in `main.tsx`), which keeps route, params, and search type-checked.
 */
export function ariaRouterProps(router: AnyRouter) {
    return {
        navigate: (
            href: ToOptions,
            routerOptions: Omit<NavigateOptions, keyof ToOptions> | undefined,
        ) => {
            void router.navigate({ ...href, ...routerOptions });
        },
        // React Aria resolves `props.href ?? ''` for every link-capable component,
        // so hrefless ones arrive here as an empty string rather than an object.
        useHref: (href: ToOptions) =>
            typeof href === 'string' ? href : router.buildLocation(href).href,
    };
}
