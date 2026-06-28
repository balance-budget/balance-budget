import { Button } from 'react-aria-components';
import { Icon, type IconName } from '../Icon';
import { cx } from '../../lib/cx';

/** A single per-row action (edit, delete, …) rendered as an icon button. */
export type RowAction = {
    icon: IconName;
    /** Accessible label / tooltip. Also used as the React key. */
    label: string;
    onPress: () => void;
    isDisabled?: boolean;
    /** When disabled, replaces `label` to explain why (e.g. "in use"). */
    disabledReason?: string;
    /** Red hover, for destructive actions. */
    danger?: boolean;
};

/**
 * Consistent right-aligned cluster of per-row icon actions for tables. Use this
 * everywhere a table row exposes actions so edit/delete read identically across
 * screens (ADR-0035). Drop it into a trailing, right-aligned `<Cell>`.
 *
 * The buttons are React Aria `Button`s so a row's `onRowAction` (navigation)
 * never fires when an action is pressed.
 */
export function RowActions({ actions }: { actions: RowAction[] }) {
    return (
        <div className="flex items-center justify-end gap-0.5">
            {actions.map(action => (
                <Button
                    key={action.label}
                    onPress={action.onPress}
                    isDisabled={action.isDisabled}
                    aria-label={
                        action.isDisabled && action.disabledReason
                            ? action.disabledReason
                            : action.label
                    }
                    className={cx(
                        'inline-flex items-center justify-center p-2 rounded-lg text-fg-3 outline-none cursor-pointer',
                        'data-[hovered]:bg-surface-2 data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary',
                        'disabled:opacity-40 disabled:cursor-default',
                        action.danger ? 'data-[hovered]:text-danger' : 'data-[hovered]:text-fg-1',
                    )}
                >
                    <Icon name={action.icon} size={16} strokeWidth={2} />
                </Button>
            ))}
        </div>
    );
}
