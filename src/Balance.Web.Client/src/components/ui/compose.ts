import { composeRenderProps } from 'react-aria-components';
import { cx } from '../../lib/cx';

/**
 * Prepends the kit's base Tailwind classes to a consumer-supplied `className`,
 * preserving React Aria's render-prop form (`className` may be a function of
 * the component's state). Consumers extend, never replace, the Balance chrome.
 */
export function composeTailwindRenderProps<T>(
    className: string | ((values: T) => string) | undefined,
    tailwind: string,
): string | ((values: T) => string) {
    return composeRenderProps(className, userClassName => cx(tailwind, userClassName));
}
