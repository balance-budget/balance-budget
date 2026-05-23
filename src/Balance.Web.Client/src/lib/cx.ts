/**
 * Conditional className concatenation. Falsy parts are dropped, so callers can
 * inline `condition && 'class'` without wrapping in a ternary that returns ''.
 * Equivalent to the popular `clsx` package — pulled in-tree to avoid the dep.
 */
export function cx(...parts: (string | false | null | undefined)[]): string {
    return parts.filter(Boolean).join(' ');
}
