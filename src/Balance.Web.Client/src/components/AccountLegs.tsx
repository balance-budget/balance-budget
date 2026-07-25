import type { AccountId } from '../lib/domain';
import { AccountLabel } from './AccountLabel';

/** The shape both a register row's counter legs and a journal projection's from/to
 *  legs already have. */
export type AccountLeg = {
    accountId: AccountId;
    accountName: string;
};

/**
 * One side of a journal entry: the first account's path, plus a `+N` count when more
 * accounts share that side. The count is `shrink-0`, so truncation never eats it and
 * leaves a row looking like a single-account entry.
 *
 * Always leads with the dot: legs render in table rows and in one-line summaries, both
 * of which take the dot (ADR-0039).
 */
export function AccountLegs({ legs }: { legs: readonly AccountLeg[] }) {
    const first = legs[0];
    if (!first) {
        return <span className="text-fg-3">—</span>;
    }

    const extra = legs.length - 1;
    return (
        <span className="flex items-center gap-1 min-w-0">
            <AccountLabel
                accountId={first.accountId}
                fallbackName={first.accountName}
                glyph="dot"
            />
            {extra > 0 && <span className="shrink-0 text-fg-3">+{extra}</span>}
        </span>
    );
}
