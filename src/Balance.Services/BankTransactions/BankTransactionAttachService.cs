using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.BankTransactions;

internal sealed class BankTransactionAttachService : IBankTransactionAttachService
{
    /// <summary>
    /// The strict 7-day window for the auto-hint (ADR 0013 condition 4).
    /// </summary>
    private const int HintDateWindowDays = 7;

    private readonly BalanceDbContext _dbContext;
    private readonly IJournalEntryService _journalEntryService;
    private readonly TimeProvider _timeProvider;

    public BankTransactionAttachService(
        BalanceDbContext dbContext,
        IJournalEntryService journalEntryService,
        TimeProvider timeProvider
    )
    {
        _dbContext = dbContext;
        _journalEntryService = journalEntryService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<JournalEntryDetailOutput>> AttachAsync(
        BankTransactionId id,
        JournalEntryId journalEntryId,
        CancellationToken cancellationToken
    )
    {
        var bankTransaction = await _dbContext.BankTransactions.FirstOrDefaultAsync(
            b => b.Id == id,
            cancellationToken
        );
        if (bankTransaction is null)
            return new NotFoundError("BankTransaction", id.Value.ToString());

        var entry = await _dbContext
            .JournalEntries.Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == journalEntryId, cancellationToken);
        if (entry is null)
            return new NotFoundError("JournalEntry", journalEntryId.Value.ToString());

        var bankAccount = await _dbContext.BankAccounts.FirstOrDefaultAsync(
            b => b.Id == bankTransaction.BankAccountId,
            cancellationToken
        );
        if (bankAccount is null || bankAccount.AccountId is null)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionRequiresOwnAccount,
                "BankTransaction's BankAccount must own an Account."
            );
        }

        var predicate = await EvaluatePredicateAsync(
            bankTransaction,
            bankAccount,
            entry,
            cancellationToken
        );
        if (predicate.IsFailure)
            return predicate.Error;

        var matchingLine = predicate.Value;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        bankTransaction.JournalEntryId = entry.Id;
        bankTransaction.UpdatedAt = now;
        matchingLine.ReconciliationStatus = ReconciliationStatus.Cleared;
        matchingLine.UpdatedAt = now;
        entry.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await _journalEntryService.GetAsync(entry.Id, cancellationToken);
    }

    public async Task<Result<JournalEntryDetailOutput>> DetachAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    )
    {
        var bankTransaction = await _dbContext.BankTransactions.FirstOrDefaultAsync(
            b => b.Id == id,
            cancellationToken
        );
        if (bankTransaction is null)
            return new NotFoundError("BankTransaction", id.Value.ToString());

        if (bankTransaction.JournalEntryId is not { } journalEntryId)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionNotAttached,
                "BankTransaction is not attached to a JournalEntry."
            );
        }

        var bankAccount = await _dbContext.BankAccounts.FirstOrDefaultAsync(
            b => b.Id == bankTransaction.BankAccountId,
            cancellationToken
        );
        if (bankAccount is null || bankAccount.AccountId is not { } bankSideAccountId)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionRequiresOwnAccount,
                "BankTransaction's BankAccount must own an Account."
            );
        }

        var entry = await _dbContext
            .JournalEntries.Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == journalEntryId, cancellationToken);
        if (entry is null)
            return new NotFoundError("JournalEntry", journalEntryId.Value.ToString());

        // Find the bank-side line on the same Account with matching Amount; flip Cleared → Uncleared.
        // Match by AccountId + Amount + currently-Cleared so multi-BT self-transfers detach the right
        // line (each BT's bank-side line is uniquely identified by its Amount on the bank-side Account).
        var matchingLine = entry.Lines.FirstOrDefault(l =>
            l.AccountId == bankSideAccountId
            && l.Amount == bankTransaction.Money.Amount
            && l.ReconciliationStatus == ReconciliationStatus.Cleared
        );

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken
        );

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        bankTransaction.JournalEntryId = null;
        bankTransaction.UpdatedAt = now;
        if (matchingLine is not null)
        {
            matchingLine.ReconciliationStatus = ReconciliationStatus.Uncleared;
            matchingLine.UpdatedAt = now;
        }
        entry.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await _journalEntryService.GetAsync(entry.Id, cancellationToken);
    }

    public async Task<AttachHintOutput?> ComputeHintAsync(
        BankTransactionId id,
        CancellationToken cancellationToken
    )
    {
        var bankTransaction = await _dbContext
            .BankTransactions.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bankTransaction is null)
            return null;
        if (bankTransaction.JournalEntryId is not null || bankTransaction.DismissedAt is not null)
            return null;
        if (string.IsNullOrWhiteSpace(bankTransaction.CounterpartyAccountNumber))
            return null;

        var bankAccount = await _dbContext
            .BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bankTransaction.BankAccountId, cancellationToken);
        if (bankAccount is null || bankAccount.AccountId is not { } bankSideAccountId)
            return null;

        var candidates = await FindMatchingEntriesAsync(
            bankTransaction,
            bankSideAccountId,
            HintDateWindowDays,
            cancellationToken
        );

        if (candidates.Count != 1)
            return null;

        var match = candidates[0];
        return new AttachHintOutput(
            match.Id,
            match.Date,
            match.Description,
            match.OtherAccountName
        );
    }

    public async Task<Result<IReadOnlyList<AttachCandidateOutput>>> ListCandidatesAsync(
        BankTransactionId id,
        int dateWindowDays,
        CancellationToken cancellationToken
    )
    {
        if (dateWindowDays < 0)
        {
            return new InvariantError(
                ErrorCodes.RequestInvalid,
                "dateWindowDays must be non-negative."
            );
        }

        var bankTransaction = await _dbContext
            .BankTransactions.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (bankTransaction is null)
            return new NotFoundError("BankTransaction", id.Value.ToString());

        var bankAccount = await _dbContext
            .BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bankTransaction.BankAccountId, cancellationToken);
        if (bankAccount is null || bankAccount.AccountId is not { } bankSideAccountId)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionRequiresOwnAccount,
                "BankTransaction's BankAccount must own an Account."
            );
        }

        var matches = await FindCandidateEntriesAsync(
            bankTransaction,
            bankSideAccountId,
            dateWindowDays,
            cancellationToken
        );
        return new Result<IReadOnlyList<AttachCandidateOutput>>(matches);
    }

    /// <summary>
    /// Walks the seven predicate conditions from ADR 0013. Returns the matching <c>JournalLine</c>
    /// on success — the bank-side <c>Uncleared</c> line that Attach will flip to <c>Cleared</c>.
    /// Failures surface as <c>InvariantError</c> with stable codes so the SPA can distinguish
    /// "wrong shape" (silent — no hint shown) from "user-explicit attempt failed" (toast).
    /// </summary>
    private async Task<Result<JournalLine>> EvaluatePredicateAsync(
        BankTransaction bankTransaction,
        BankAccount bankAccount,
        JournalEntry entry,
        CancellationToken cancellationToken
    )
    {
        // (1) BT must be uncategorised and not dismissed.
        if (bankTransaction.JournalEntryId is not null)
        {
            return new ConflictError(
                ErrorCodes.BankTransactionAlreadyCategorised,
                "BankTransaction is already attached to a JournalEntry."
            );
        }
        if (bankTransaction.DismissedAt is not null)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionDismissed,
                "BankTransaction is dismissed; undismiss before attaching."
            );
        }

        if (bankAccount.AccountId is not { } bankSideAccountId)
        {
            return new InvariantError(
                ErrorCodes.BankTransactionRequiresOwnAccount,
                "BankTransaction must originate from a BankAccount that owns an Account."
            );
        }

        // (7) JE.CounterpartyId is null.
        if (entry.CounterpartyId is not null)
        {
            return new InvariantError(
                ErrorCodes.AttachPredicateFailed,
                "JournalEntry has a Counterparty; only self-transfer entries can be attached."
            );
        }

        // (4) |BT.BookingDate - JE.Date| <= 7 days.
        if (
            Math.Abs(bankTransaction.BookingDate.DayNumber - entry.Date.DayNumber)
            > HintDateWindowDays
        )
        {
            return new InvariantError(
                ErrorCodes.AttachPredicateFailed,
                $"JournalEntry date is more than {HintDateWindowDays} days from the BankTransaction's BookingDate."
            );
        }

        // Load Account currency/type info for every line on the entry so we can vet (2), (5), (6).
        var accountIds = entry.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        // Load BankAccounts keyed by AccountId (every Account has at most one). (6) requires every
        // line's Account to have a backing BankAccount, i.e. own-Account.
        var bankAccountsByAccountId = await _dbContext
            .BankAccounts.AsNoTracking()
            .Where(b => b.AccountId != null && accountIds.Contains(b.AccountId!.Value))
            .ToDictionaryAsync(b => b.AccountId!.Value, cancellationToken);

        // (6) every line is on an own-Account (self-transfer shape guard).
        foreach (var line in entry.Lines)
        {
            if (!bankAccountsByAccountId.ContainsKey(line.AccountId))
            {
                return new InvariantError(
                    ErrorCodes.AttachSelfTransferGuard,
                    "JournalEntry has a line whose Account is not backed by a BankAccount — "
                        + "only self-transfer entries (every line on an own-Account) accept Attach."
                );
            }
        }

        // (2) find L1: Uncleared, on the bank-side Account, amount matches BT.Amount.
        // The sign-aligned-in-the-focal-account-frame rule: positive BT = money in to BankAccount,
        // which on an Asset line is a positive (debit) Amount, on a Liability line is a negative
        // (credit) Amount. With the line-Amount convention used in the codebase (+ for asset
        // debit, - for asset credit; symmetric for liability), a bank-side line whose Amount
        // equals BT.Amount captures both senses uniformly because the BT.Amount sign already
        // encodes the in/out direction for the owning Account.
        //
        // Raw amount equality is deliberate (not a missing AccountSignConvention consult): the
        // counter-side line was created in the Categorisation flow as a verbatim copy of the first
        // BT's Amount, so a self-transfer matches iff the two bank statements report equal-and-
        // opposite signed amounts. This holds when both extractors emit the "money-in-positive"
        // convention; for Liability-backed own-Accounts (Card pay-downs) it relies on ING's
        // credit-card CSV signing the DirectDebit row opposite to the funding current-account row.
        // That cross-statement invariant is treated as known-good — if revisited, pin it with a
        // Current->Card round-trip integration test rather than changing this predicate.
        var candidateLines = entry
            .Lines.Where(l =>
                l.AccountId == bankSideAccountId
                && l.ReconciliationStatus == ReconciliationStatus.Uncleared
                && l.Amount == bankTransaction.Money.Amount
            )
            .ToList();
        if (candidateLines.Count == 0)
        {
            return new InvariantError(
                ErrorCodes.AttachPredicateFailed,
                "JournalEntry has no Uncleared line on the BankTransaction's Account with a matching Amount."
            );
        }
        if (candidateLines.Count > 1)
        {
            return new InvariantError(
                ErrorCodes.AttachPredicateFailed,
                "JournalEntry has more than one Uncleared line that could match — ambiguous."
            );
        }
        var l1 = candidateLines[0];

        // (5) currency match: the bank-side line's Account currency == BT currency.
        if (!accounts.TryGetValue(l1.AccountId, out var l1Account))
        {
            return new InvariantError(
                ErrorCodes.AttachPredicateFailed,
                "Bank-side Account vanished mid-validation."
            );
        }
        if (l1Account.CurrencyCode.Value != bankTransaction.Money.CurrencyCode.Value)
        {
            return new InvariantError(
                ErrorCodes.AttachPredicateFailed,
                "Bank-side Account currency does not match BankTransaction currency."
            );
        }

        // (3) there exists L2 on the same JE whose Account has a backing BankAccount with Iban
        // == BT.CounterpartyAccountNumber.
        if (string.IsNullOrWhiteSpace(bankTransaction.CounterpartyAccountNumber))
        {
            return new InvariantError(
                ErrorCodes.AttachPredicateFailed,
                "BankTransaction has no CounterpartyAccountNumber — cannot match a counter-side line."
            );
        }

        var counterIban = bankTransaction.CounterpartyAccountNumber;
        var hasCounterLine = entry.Lines.Any(l =>
            l.Id != l1.Id
            && bankAccountsByAccountId.TryGetValue(l.AccountId, out var ba)
            && ba.Iban == counterIban
        );
        if (!hasCounterLine)
        {
            return new InvariantError(
                ErrorCodes.AttachPredicateFailed,
                "JournalEntry has no line on an Account backed by a BankAccount with the BT's CounterpartyAccountNumber."
            );
        }

        return new Result<JournalLine>(l1);
    }

    /// <summary>
    /// Read-side equivalent of the predicate for the Inbox hint: enumerates every JournalEntry
    /// that satisfies all 7 conditions, projecting the compact summary the hint needs. Returns
    /// the set in arbitrary order — callers decide what "exactly one" means.
    /// </summary>
    private async Task<IReadOnlyList<AttachHintOutput>> FindMatchingEntriesAsync(
        BankTransaction bankTransaction,
        AccountId bankSideAccountId,
        int dateWindowDays,
        CancellationToken cancellationToken
    )
    {
        var matches = await FindSelfTransferCandidatesAsync(
            bankTransaction,
            bankSideAccountId,
            dateWindowDays,
            requireCounterIbanMatch: true,
            cancellationToken
        );
        return matches
            .Select(m => new AttachHintOutput(m.Id, m.Date, m.Description, m.OtherAccountName))
            .ToList();
    }

    /// <summary>
    /// Manual-picker variant of <see cref="FindMatchingEntriesAsync"/>: keeps the structural
    /// conditions (own-Account-only, currency match, available Uncleared bank-side slot) but
    /// drops the strict-counter-IBAN match — the user wants to see entries even when their IBAN
    /// parsing missed. The date window is the user's choice; results are ordered by date proximity.
    /// </summary>
    private async Task<IReadOnlyList<AttachCandidateOutput>> FindCandidateEntriesAsync(
        BankTransaction bankTransaction,
        AccountId bankSideAccountId,
        int dateWindowDays,
        CancellationToken cancellationToken
    )
    {
        var matches = await FindSelfTransferCandidatesAsync(
            bankTransaction,
            bankSideAccountId,
            dateWindowDays,
            requireCounterIbanMatch: false,
            cancellationToken
        );
        return matches
            .OrderBy(m => Math.Abs(m.Date.DayNumber - bankTransaction.BookingDate.DayNumber))
            .ThenByDescending(m => m.Date)
            .Select(m => new AttachCandidateOutput(
                m.Id,
                m.Date,
                m.Description,
                m.OtherAccountName,
                m.CounterAmount
            ))
            .ToList();
    }

    /// <summary>
    /// Shared engine behind the Inbox hint (<see cref="FindMatchingEntriesAsync"/>) and the manual
    /// JE-picker (<see cref="FindCandidateEntriesAsync"/>). Both must agree on the structural
    /// self-transfer predicate (own-Account-only, currency match, an available Uncleared bank-side
    /// line of matching amount); the only intended difference is whether the counter-side line is
    /// pinned to the BT's CounterpartyAccountNumber (the strict hint) or left free (the picker).
    /// Keeping them in one method is what keeps them from drifting apart.
    /// </summary>
    private async Task<List<CandidateMatch>> FindSelfTransferCandidatesAsync(
        BankTransaction bankTransaction,
        AccountId bankSideAccountId,
        int dateWindowDays,
        bool requireCounterIbanMatch,
        CancellationToken cancellationToken
    )
    {
        var minDate = bankTransaction.BookingDate.AddDays(-dateWindowDays);
        var maxDate = bankTransaction.BookingDate.AddDays(dateWindowDays);
        var amount = bankTransaction.Money.Amount;
        var currency = bankTransaction.Money.CurrencyCode.Value;
        var counterIban = bankTransaction.CounterpartyAccountNumber;

        // Candidate entries: date in window, no counterparty, with an Uncleared line on the
        // bank-side Account of the right amount. The structural own-Account guard is refined in
        // memory below (it would be ugly as raw EF).
        var query = _dbContext
            .JournalEntries.AsNoTracking()
            .Where(e =>
                e.CounterpartyId == null
                && e.Date >= minDate
                && e.Date <= maxDate
                && e.Lines.Any(l =>
                    l.AccountId == bankSideAccountId
                    && l.ReconciliationStatus == ReconciliationStatus.Uncleared
                    && l.Amount == amount
                )
            );

        // Strict hint only: the entry must also contain a line whose Account has a BankAccount
        // with Iban == counterIban. The picker drops this so near-misses still surface.
        if (requireCounterIbanMatch)
        {
            query = query.Where(e =>
                e.Lines.Any(l =>
                    _dbContext.BankAccounts.Any(b =>
                        b.AccountId == l.AccountId && b.Iban == counterIban
                    )
                )
            );
        }

        var candidates = await query
            .Select(e => new
            {
                e.Id,
                e.Date,
                e.Description,
                Lines = e
                    .Lines.Select(l => new
                    {
                        l.Id,
                        l.AccountId,
                        l.Amount,
                        l.ReconciliationStatus,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return [];

        // Resolve own-Account guard + currency + counter-side name in memory.
        var allAccountIds = candidates
            .SelectMany(c => c.Lines.Select(l => l.AccountId))
            .Distinct()
            .ToList();
        var accounts = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => allAccountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);
        var bankAccountsByAccountId = await _dbContext
            .BankAccounts.AsNoTracking()
            .Where(b => b.AccountId != null && allAccountIds.Contains(b.AccountId!.Value))
            .ToDictionaryAsync(b => b.AccountId!.Value, cancellationToken);

        var matches = new List<CandidateMatch>();
        foreach (var candidate in candidates)
        {
            var l1 = candidate.Lines.SingleOrDefault(l =>
                l.AccountId == bankSideAccountId
                && l.ReconciliationStatus == ReconciliationStatus.Uncleared
                && l.Amount == amount
            );
            if (l1 is null)
                continue;
            if (!accounts.TryGetValue(l1.AccountId, out var l1Account))
                continue;
            if (l1Account.CurrencyCode.Value != currency)
                continue;

            // every line on own-Account
            if (candidate.Lines.Any(l => !bankAccountsByAccountId.ContainsKey(l.AccountId)))
                continue;

            var counterLine = requireCounterIbanMatch
                ? candidate.Lines.FirstOrDefault(l =>
                    l.Id != l1.Id
                    && bankAccountsByAccountId.TryGetValue(l.AccountId, out var ba)
                    && ba.Iban == counterIban
                )
                : candidate.Lines.FirstOrDefault(l => l.Id != l1.Id);
            if (counterLine is null)
                continue;

            var otherAccount = accounts[counterLine.AccountId];
            matches.Add(
                new CandidateMatch(
                    candidate.Id,
                    candidate.Date,
                    candidate.Description,
                    otherAccount.Name,
                    counterLine.Amount
                )
            );
        }
        return matches;
    }

    /// <summary>
    /// Common intermediate produced by <see cref="FindSelfTransferCandidatesAsync"/>; carries every
    /// field both the hint and the picker outputs need (the picker also surfaces the counter amount).
    /// </summary>
    private sealed record CandidateMatch(
        JournalEntryId Id,
        DateOnly Date,
        string? Description,
        string OtherAccountName,
        long CounterAmount
    );
}
