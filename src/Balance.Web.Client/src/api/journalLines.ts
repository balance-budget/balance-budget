import { useMutation, useQueryClient } from '@tanstack/react-query';
import { accountsKeys } from './accounts';
import { journalEntriesKeys } from './journalEntries';
import type { AccountId, JournalLineId } from '../lib/domain';
import { postJsonNoContent } from '../lib/http';

/**
 * Bulk reassign: re-point the selected journal lines to another postable account of
 * the same currency, all-or-nothing (see CONTEXT.md "Reassign"). Invalidates the
 * accounts tree (registers + balances) and the journal entry lists on success.
 */
export function useReassignJournalLines() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: {
            lineIds: readonly JournalLineId[];
            targetAccountId: AccountId;
        }) => {
            await postJsonNoContent(
                '/api/journal-lines/reassign',
                { lineIds: args.lineIds, targetAccountId: args.targetAccountId },
                'reassign journal lines',
            );
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: accountsKeys.all });
            await queryClient.invalidateQueries({ queryKey: journalEntriesKeys.all });
        },
    });
}
