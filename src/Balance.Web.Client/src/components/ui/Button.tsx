import { Button as AriaButton, type ButtonProps as AriaButtonProps } from 'react-aria-components';
import { composeTailwindRenderProps } from './compose';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';

const BASE =
    'inline-flex h-9 items-center justify-center gap-1.5 px-3 rounded-lg text-sm font-medium ' +
    'outline-none data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary disabled:opacity-60';

const VARIANT: Record<ButtonVariant, string> = {
    primary:
        'text-white bg-brand-primary data-[hovered]:bg-brand-primary-dark data-[pressed]:bg-brand-primary-dark',
    secondary:
        'text-fg-2 bg-surface-2 border border-border-soft data-[hovered]:text-fg-1 data-[hovered]:bg-surface-3 data-[pressed]:bg-surface-3',
    ghost: 'text-fg-2 data-[hovered]:text-fg-1 data-[hovered]:bg-surface-2 data-[pressed]:bg-surface-2',
    danger: 'text-white bg-danger data-[hovered]:bg-danger-strong data-[pressed]:bg-danger-strong',
};

export type ButtonProps = AriaButtonProps & {
    variant?: ButtonVariant;
};

export function Button({ variant = 'secondary', ...props }: ButtonProps) {
    return (
        <AriaButton
            {...props}
            className={composeTailwindRenderProps(props.className, `${BASE} ${VARIANT[variant]}`)}
        />
    );
}

/** Bare icon button — used for field adornments and modal close affordances. */
export function IconButton(props: AriaButtonProps) {
    return (
        <AriaButton
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'inline-flex items-center justify-center p-1 rounded-lg text-fg-3 outline-none ' +
                    'data-[hovered]:text-fg-1 data-[hovered]:bg-surface-2 ' +
                    'data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary disabled:opacity-60',
            )}
        />
    );
}
