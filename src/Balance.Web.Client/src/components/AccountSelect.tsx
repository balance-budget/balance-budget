import { useMemo } from 'react';
import { useAccounts, type Account } from '../api/accounts';
import {
    ACCOUNT_PATH_SEPARATOR,
    accountPathSegments,
    descendantAndSelfIds,
} from '../lib/accountTree';
import {
    ACCOUNT_TYPE_LABEL,
    ACCOUNT_TYPE_ORDER,
    type AccountId,
    type AccountType,
} from '../lib/domain';
import { ComboBox } from './ui/ComboBox';
import type { ComboBoxItem } from './ui/combobox.state';

// Deep paths ("5131  Car › Insurance › Liability › Excess") need more room than
// the narrow filter/inline triggers give, so the open list widens to at least
// this regardless of the trigger width (capped to the viewport in ComboBox).
const LISTBOX_MIN_WIDTH = 360;

/**
 * The single account picker used everywhere in the app. Wraps `<ComboBox>` and
 * owns the rules the selectors must share (ADR-0019): each option shows the
 * account code as a muted prefix and the full path with the ancestors dimmed
 * ("5110  Car › Tax"), options are grouped by AccountType and sorted by code,
 * and typing matches the code or any path segment. Which accounts are offered
 * is set by the filter props — the three call-site classes are:
 *
 *  - posting target → `postableOnly` (only leaves a JournalLine may reference)
 *  - filter         → no postability prop (placeholders mean "this subtree")
 *  - parent picker  → `placeholdersOnly` (a parent is always non-postable)
 *
 * Accounts are read from the shared `useAccounts()` cache, so call sites pass
 * filter props, never a pre-built list — that's what keeps the pickers from
 * drifting apart.
 */
export type AccountSelectProps = {
    value: AccountId | null;
    onChange: (id: AccountId) => void;
    /** Selecting the "None" sentinel. Required when `noneLabel` is set. */
    onClear?: () => void;
    /** Offer only postable leaves — posting-target pickers. */
    postableOnly?: boolean;
    /** Offer only non-postable placeholders — the parent picker. */
    placeholdersOnly?: boolean;
    /** Restrict to one currency. The current value stays visible regardless. */
    currencyCode?: string;
    /** Pin a single AccountType — the parent picker shares the child's type. */
    type?: AccountType;
    /** Drop these accounts (e.g. a transaction's own bank-side leg, or self). */
    exclude?: readonly AccountId[];
    /** Offer only strict descendants of this account — the sub-account filter. */
    subtreeOf?: AccountId;
    /** Drop this account and its whole subtree — the parent picker's cycle guard. */
    excludeSubtreeOf?: AccountId;
    noneLabel?: string;
    placeholder?: string;
    disabled?: boolean;
    ariaLabel?: string;
    /** Field name for React Aria Form `validationErrors`. */
    name?: string;
};

function toItem(account: Account, byId: ReadonlyMap<AccountId, Account>): ComboBoxItem<AccountId> {
    const segments = accountPathSegments(byId, account.id);
    const leaf = segments[segments.length - 1];
    const ancestors = segments.slice(0, -1);
    const path = segments.join(ACCOUNT_PATH_SEPARATOR);
    return {
        key: account.id,
        value: account.id,
        group: account.type,
        // Collapsed/selected display.
        label: `${account.code}  ${path}`,
        // Space-joined so "car t" matches "Car › Tax" and "5110" jumps straight to it.
        searchText: `${account.code} ${segments.join(' ')}`,
        render: (
            <>
                <span className="text-fg-3 tabular-nums mr-2">{account.code}</span>
                {ancestors.length > 0 && (
                    <span className="text-fg-3">
                        {ancestors.join(ACCOUNT_PATH_SEPARATOR)}
                        {ACCOUNT_PATH_SEPARATOR}
                    </span>
                )}
                <span>{leaf}</span>
            </>
        ),
    };
}

export function AccountSelect({
    value,
    onChange,
    onClear,
    postableOnly,
    placeholdersOnly,
    currencyCode,
    type,
    exclude,
    subtreeOf,
    excludeSubtreeOf,
    noneLabel,
    placeholder,
    disabled,
    ariaLabel,
    name,
}: AccountSelectProps) {
    const accounts = useAccounts();
    const all = useMemo(() => accounts.data ?? [], [accounts.data]);
    const byId = useMemo(() => new Map(all.map(a => [a.id, a])), [all]);

    // Inline arrays would re-run the memo every render; key on the contents.
    const excludeKey = (exclude ?? []).join(',');

    const items = useMemo<ComboBoxItem<AccountId>[]>(() => {
        const subtree = subtreeOf ? descendantAndSelfIds(all, subtreeOf) : null;
        const excludedSubtree = excludeSubtreeOf
            ? descendantAndSelfIds(all, excludeSubtreeOf)
            : null;
        const excludeSet = new Set(excludeKey === '' ? [] : (excludeKey.split(',') as AccountId[]));

        const selectable = all.filter(a => {
            if (postableOnly && !a.isPostable) return false;
            if (placeholdersOnly && a.isPostable) return false;
            if (type && a.type !== type) return false;
            if (currencyCode && a.currencyCode !== currencyCode) return false;
            if (excludeSet.has(a.id)) return false;
            // subtreeOf: strict descendants only (the viewed account is the page itself).
            if (subtree && (!subtree.has(a.id) || a.id === subtreeOf)) return false;
            if (excludedSubtree?.has(a.id)) return false;
            return true;
        });

        // Always keep the current selection visible, even if a filter would hide
        // it (e.g. an already-chosen leg in another currency).
        if (value !== null && !selectable.some(a => a.id === value)) {
            const current = byId.get(value);
            if (current) selectable.push(current);
        }

        return selectable
            .sort((a, b) => a.code.localeCompare(b.code, undefined, { numeric: true }))
            .map(a => toItem(a, byId));
    }, [
        all,
        byId,
        value,
        postableOnly,
        placeholdersOnly,
        type,
        currencyCode,
        excludeKey,
        subtreeOf,
        excludeSubtreeOf,
    ]);

    return (
        <ComboBox
            items={items}
            value={value}
            onChange={onChange}
            onClear={onClear}
            noneLabel={noneLabel}
            groupOrder={ACCOUNT_TYPE_ORDER}
            groupLabels={ACCOUNT_TYPE_LABEL}
            placeholder={placeholder}
            disabled={disabled}
            ariaLabel={ariaLabel}
            name={name}
            listboxMinWidth={LISTBOX_MIN_WIDTH}
        />
    );
}
