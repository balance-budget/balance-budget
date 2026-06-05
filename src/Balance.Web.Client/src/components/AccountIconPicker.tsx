import { useEffect, useRef, useState } from 'react';
import type { AccountType } from '../lib/domain';
import { cx } from '../lib/cx';
import { ACCOUNT_ICON_CHOICES, visualHintFor } from '../lib/visualHints';
import { AccountAvatar } from './AccountAvatar';
import { Icon } from './Icon';

type AccountIconPickerProps = {
    /** Drives the avatar tint and the "Default" icon — the picker re-tints live as the form's Type changes. */
    accountType: AccountType;
    /** The chosen icon name, or null for the AccountType default. */
    value: string | null;
    onChange: (icon: string | null) => void;
};

/**
 * Avatar-shaped trigger that opens a popover grid of the curated account icons
 * (ACCOUNT_ICON_CHOICES) plus a "Default" choice that clears back to the
 * AccountType's icon. Colours are never picked here — the tint always follows
 * the AccountType.
 */
export function AccountIconPicker({ accountType, value, onChange }: AccountIconPickerProps) {
    const [open, setOpen] = useState(false);
    const wrapperRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        if (!open) return;
        function onDocClick(e: MouseEvent) {
            if (wrapperRef.current?.contains(e.target as Node)) return;
            setOpen(false);
        }
        function onKeyDown(e: KeyboardEvent) {
            if (e.key === 'Escape') setOpen(false);
        }
        document.addEventListener('mousedown', onDocClick);
        document.addEventListener('keydown', onKeyDown);
        return () => {
            document.removeEventListener('mousedown', onDocClick);
            document.removeEventListener('keydown', onKeyDown);
        };
    }, [open]);

    const { accentColor, iconName: defaultIconName } = visualHintFor({
        type: accountType,
        icon: null,
    });

    function commit(icon: string | null) {
        onChange(icon);
        setOpen(false);
    }

    return (
        <div ref={wrapperRef} className="relative">
            <button
                type="button"
                aria-haspopup="listbox"
                aria-expanded={open}
                onClick={() => {
                    setOpen(o => !o);
                }}
                className="flex items-center gap-2 p-1 -m-1 rounded-sm hover:bg-surface-2"
            >
                <AccountAvatar account={{ type: accountType, icon: value }} size="md" />
                <span className="text-12 text-fg-3">
                    {value === null ? 'Default' : 'Custom'} — click to change
                </span>
            </button>
            {open && (
                <div
                    role="listbox"
                    aria-label="Account icon"
                    className={cx(
                        'absolute top-full left-0 mt-1 z-50 w-[296px] p-2',
                        'bg-bg-1 border border-border-soft rounded-sm shadow-overlay',
                    )}
                >
                    <button
                        type="button"
                        role="option"
                        aria-selected={value === null}
                        onClick={() => {
                            commit(null);
                        }}
                        className={cx(
                            'w-full flex items-center gap-2 px-2 py-[6px] mb-1 rounded-sm text-12',
                            value === null
                                ? 'bg-brand-primary-soft text-brand-primary'
                                : 'text-fg-2 hover:bg-surface-2',
                        )}
                    >
                        <Icon name={defaultIconName} size={14} strokeWidth={1.75} />
                        <span>Default for {accountType}</span>
                    </button>
                    <div className="grid grid-cols-8 gap-1">
                        {ACCOUNT_ICON_CHOICES.map(icon => {
                            const selected = icon === value;
                            return (
                                <button
                                    key={icon}
                                    type="button"
                                    role="option"
                                    aria-selected={selected}
                                    title={icon}
                                    onClick={() => {
                                        commit(icon);
                                    }}
                                    style={{
                                        color: accentColor,
                                        background: selected
                                            ? `color-mix(in srgb, ${accentColor} 16%, transparent)`
                                            : undefined,
                                    }}
                                    className={cx(
                                        'w-8 h-8 inline-flex items-center justify-center rounded-sm',
                                        !selected && 'hover:bg-surface-2',
                                    )}
                                >
                                    <Icon name={icon} size={16} strokeWidth={1.75} />
                                </button>
                            );
                        })}
                    </div>
                </div>
            )}
        </div>
    );
}
