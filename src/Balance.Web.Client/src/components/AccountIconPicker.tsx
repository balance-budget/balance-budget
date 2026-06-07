import { useState } from 'react';
import { Button, DialogTrigger, ListBox, ListBoxItem } from 'react-aria-components';
import type { AccountType } from '../lib/domain';
import { cx } from '../lib/cx';
import { ACCOUNT_ICON_CHOICES, visualHintFor } from '../lib/visualHints';
import { AccountAvatar } from './AccountAvatar';
import { Icon } from './Icon';
import { Popover } from './ui/Popover';
import { selectedKey } from './ui/selection';

type AccountIconPickerProps = {
    /** Drives the avatar tint and the "Default" icon — the picker re-tints live as the form's Type changes. */
    accountType: AccountType;
    /** The chosen icon name, or null for the AccountType default. */
    value: string | null;
    onChange: (icon: string | null) => void;
};

/** Sentinel id for the "Default" choice — clears back to the AccountType icon. */
const DEFAULT_KEY = '__default__';

/**
 * Avatar-shaped trigger that opens a popover grid of the curated account icons
 * (ACCOUNT_ICON_CHOICES) plus a "Default" choice that clears back to the
 * AccountType's icon. Colours are never picked here — the tint always follows
 * the AccountType.
 */
export function AccountIconPicker({ accountType, value, onChange }: AccountIconPickerProps) {
    const [open, setOpen] = useState(false);

    const { accentColor, iconName: defaultIconName } = visualHintFor({
        type: accountType,
        icon: null,
    });

    return (
        <DialogTrigger isOpen={open} onOpenChange={setOpen}>
            <Button className="flex items-center gap-2 p-1 -m-1 rounded-lg outline-none cursor-pointer data-[hovered]:bg-surface-2 data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary">
                <AccountAvatar account={{ type: accountType, icon: value }} size="md" />
                <span className="text-xs text-fg-3">
                    {value === null ? 'Default' : 'Custom'} — click to change
                </span>
            </Button>
            <Popover placement="bottom start" className="w-[296px] p-2">
                <ListBox
                    aria-label="Account icon"
                    layout="grid"
                    selectionMode="single"
                    selectedKeys={[value ?? DEFAULT_KEY]}
                    onSelectionChange={keys => {
                        const key = selectedKey(keys);
                        if (key === undefined) return;
                        onChange(key === DEFAULT_KEY ? null : String(key));
                        setOpen(false);
                    }}
                    className="grid grid-cols-8 gap-1 outline-none"
                >
                    <ListBoxItem
                        id={DEFAULT_KEY}
                        textValue={`Default for ${accountType}`}
                        className={({ isSelected }) =>
                            cx(
                                'col-span-8 flex items-center gap-2 px-2 py-[6px] mb-1 rounded-lg text-xs cursor-pointer outline-none',
                                'data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary',
                                isSelected
                                    ? 'bg-brand-primary-soft text-brand-primary'
                                    : 'text-fg-2 data-[hovered]:bg-surface-2',
                            )
                        }
                    >
                        <Icon name={defaultIconName} size={14} strokeWidth={1.75} />
                        <span>Default for {accountType}</span>
                    </ListBoxItem>
                    {ACCOUNT_ICON_CHOICES.map(icon => (
                        <ListBoxItem
                            key={icon}
                            id={icon}
                            textValue={icon}
                            style={({ isSelected }) => ({
                                color: accentColor,
                                background: isSelected
                                    ? `color-mix(in srgb, ${accentColor} 16%, transparent)`
                                    : undefined,
                            })}
                            className={({ isSelected }) =>
                                cx(
                                    'w-8 h-8 inline-flex items-center justify-center rounded-lg cursor-pointer outline-none',
                                    'data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary',
                                    !isSelected && 'data-[hovered]:bg-surface-2',
                                )
                            }
                        >
                            <Icon name={icon} size={16} strokeWidth={1.75} />
                        </ListBoxItem>
                    ))}
                </ListBox>
            </Popover>
        </DialogTrigger>
    );
}
