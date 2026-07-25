import { useAccountIndex, type Account } from '../api/accounts';
import {
    ACCOUNT_PATH_SEPARATOR,
    accountPathLabel,
    accountPathSegments,
    accountPathText,
    type AccountIndex,
} from '../lib/accountTree';
import { cx } from '../lib/cx';
import type { AccountId } from '../lib/domain';
import { ACCENT_BY_TYPE } from '../lib/visualHints';
import { AccountAvatar } from './AccountAvatar';

/** `icon` carries the account's identity (the user's own glyph); `dot` carries only
 *  its AccountType, for columns where the icon would repeat down every row. */
export type AccountLabelGlyph = 'icon' | 'dot' | 'none';

export type AccountLabelProps = {
    accountId: AccountId;
    /** The flat name the read model already carries, shown until the accounts cache
     *  resolves and whenever `accountId` isn't in it (ADR-0039). */
    fallbackName?: string | null;
    glyph?: AccountLabelGlyph;
    /** Prefix the path with the account Code — only where the column can spare ~35px. */
    showCode?: boolean;
    className?: string;
};

/**
 * An Account rendered as its full path ("Car › Insurance › Excess"), resolved from
 * the shared accounts cache — the one component every read-only account display goes
 * through (ADR-0039). The chart-of-accounts trees are the deliberate exception:
 * indentation is the path there.
 *
 * Inherits the surrounding font size and leaf color, so the same component works in a
 * dense register row and a detail panel; only the dimming is its own.
 */
export function AccountLabel({
    accountId,
    fallbackName,
    glyph = 'icon',
    showCode = false,
    className,
}: AccountLabelProps) {
    const byId = useAccountIndex();
    const account = byId.get(accountId);
    const segments = account ? accountPathSegments(byId, accountId) : [];

    // Cache cold, or an id the chart of accounts doesn't have: the flat name is all
    // there is. Rendered in the same shape so the row doesn't reflow on upgrade.
    const leaf = segments[segments.length - 1] ?? fallbackName ?? '—';
    const ancestors = segments.slice(0, -1);
    const code = showCode ? account?.code : undefined;

    return (
        <span
            className={cx('inline-flex items-center gap-1.5 min-w-0 max-w-full', className)}
            title={accountLabelText(byId, accountId, { fallbackName, showCode })}
        >
            <AccountGlyph glyph={glyph} account={account} />
            {code !== undefined && <span className="shrink-0 text-fg-3 tabular-nums">{code}</span>}
            {ancestors.length > 0 && (
                <>
                    {/* Ancestors give up their pixels first: they shrink to an ellipsis
                     *  before the leaf — the identifying segment — loses one. */}
                    <span className="truncate shrink-[9999] text-fg-3">
                        {ancestors.join(ACCOUNT_PATH_SEPARATOR)}
                    </span>
                    <span aria-hidden="true" className="shrink-0 text-fg-4">
                        ›
                    </span>
                </>
            )}
            <span className="truncate">{leaf}</span>
        </span>
    );
}

function AccountGlyph({
    glyph,
    account,
}: {
    glyph: AccountLabelGlyph;
    account: Account | undefined;
}) {
    // Nothing to tint or draw until the account resolves; the spacer keeps the text
    // from jumping sideways when it does.
    if (glyph === 'none') return null;

    if (!account) {
        return <span className={cx('shrink-0', glyph === 'dot' ? 'w-1.5' : 'w-5')} />;
    }

    if (glyph === 'dot') {
        return (
            <span
                aria-hidden="true"
                className="shrink-0 w-1.5 h-1.5 rounded-full"
                style={{ background: ACCENT_BY_TYPE[account.type] }}
            />
        );
    }

    return <AccountAvatar account={account} size="xs" />;
}

/** The untruncated path behind the `title=` reveal. Text that needs to travel
 *  (an `aria-label`, a confirmation message) uses `accountPathLabel` directly. */
function accountLabelText(
    byId: AccountIndex,
    accountId: AccountId,
    options: { fallbackName?: string | null; showCode?: boolean } = {},
): string {
    const fallback = options.fallbackName ?? '';
    return options.showCode === true
        ? (accountPathLabel(byId, accountId) ?? fallback)
        : accountPathText(byId, accountId, fallback);
}
