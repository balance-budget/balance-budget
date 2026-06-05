using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

/// <summary>
/// Covers the BankTransaction Attach feature (ADR 0012): the 7-condition predicate,
/// Attach/Detach state transitions, Inbox hint computation, and the manual JE-picker
/// candidate list (ADR 0013).
///
/// All tests seed a sibling self-transfer scenario: BT-A on the current-account is
/// categorised as a self-transfer to a savings own-account, leaving the savings line
/// Uncleared. BT-B then arrives on the savings statement; the predicate decides whether
/// BT-B may attach to BT-A's JE.
/// </summary>
internal sealed class BankTransactionAttachServiceTests : EndpointsTestsBase
{
    [Test]
    public async Task AttachAsync_happy_path_sets_link_and_flips_line_to_cleared(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedSelfTransferScenarioAsync(cancellationToken);

        var attach = await fixture.AttachService.AttachAsync(
            fixture.SiblingBtId,
            fixture.SelfTransferJeId,
            cancellationToken
        );

        await Assert.That(attach.IsSuccess).IsTrue();
        var entry = attach.Value!;
        await Assert.That(entry.BankTransactions).Count().IsEqualTo(2);
        await Assert
            .That(entry.Lines.All(l => l.ReconciliationStatus == ReconciliationStatus.Cleared))
            .IsTrue();

        using var verifyScope = Factory.Services.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var sibling = await dbContext.BankTransactions.SingleAsync(
            b => b.Id == fixture.SiblingBtId,
            cancellationToken
        );
        await Assert.That(sibling.JournalEntryId).IsEqualTo(fixture.SelfTransferJeId);
    }

    [Test]
    public async Task AttachAsync_rejects_when_bt_already_attached(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedSelfTransferScenarioAsync(cancellationToken);

        // Attach once, then re-attempt.
        var first = await fixture.AttachService.AttachAsync(
            fixture.SiblingBtId,
            fixture.SelfTransferJeId,
            cancellationToken
        );
        await Assert.That(first.IsSuccess).IsTrue();

        var second = await fixture.AttachService.AttachAsync(
            fixture.SiblingBtId,
            fixture.SelfTransferJeId,
            cancellationToken
        );
        await Assert.That(second.IsFailure).IsTrue();
        await Assert.That(second.Error).IsTypeOf<ConflictError>();
        var conflict = (ConflictError)second.Error!;
        await Assert.That(conflict.Code).IsEqualTo(ErrorCodes.BankTransactionAlreadyCategorised);
    }

    [Test]
    public async Task AttachAsync_rejects_when_bt_dismissed(CancellationToken cancellationToken)
    {
        await using var fixture = await SeedSelfTransferScenarioAsync(cancellationToken);

        var dismiss = await fixture.BankTransactionService.DismissAsync(
            fixture.SiblingBtId,
            "ignore",
            cancellationToken
        );
        await Assert.That(dismiss.IsSuccess).IsTrue();

        var attach = await fixture.AttachService.AttachAsync(
            fixture.SiblingBtId,
            fixture.SelfTransferJeId,
            cancellationToken
        );

        await Assert.That(attach.IsFailure).IsTrue();
        await Assert.That(attach.Error).IsTypeOf<InvariantError>();
        var invariant = (InvariantError)attach.Error!;
        await Assert.That(invariant.Code).IsEqualTo(ErrorCodes.BankTransactionDismissed);
    }

    [Test]
    public async Task AttachAsync_rejects_when_je_has_counterparty(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedSelfTransferScenarioAsync(cancellationToken);

        // Mutate the JE to attach a counterparty — the self-transfer guard should reject.
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
            var counterpartyService =
                scope.ServiceProvider.GetRequiredService<ICounterpartyService>();
            var cp = (
                await counterpartyService.CreateAsync(
                    $"cp-attach-{Guid.NewGuid():N}",
                    cancellationToken
                )
            ).Value!;
            var entry = await dbContext.JournalEntries.SingleAsync(
                e => e.Id == fixture.SelfTransferJeId,
                cancellationToken
            );
            entry.CounterpartyId = cp.Id;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Acquire the AttachService from a fresh scope so its DbContext loads the freshly-mutated
        // JE row (the fixture's scope still has the original Counterparty-null state cached).
        using var attachScope = Factory.Services.CreateScope();
        var attachService =
            attachScope.ServiceProvider.GetRequiredService<IBankTransactionAttachService>();

        var attach = await attachService.AttachAsync(
            fixture.SiblingBtId,
            fixture.SelfTransferJeId,
            cancellationToken
        );

        await Assert.That(attach.IsFailure).IsTrue();
        await Assert.That(attach.Error).IsTypeOf<InvariantError>();
        var invariant = (InvariantError)attach.Error!;
        await Assert.That(invariant.Code).IsEqualTo(ErrorCodes.AttachPredicateFailed);
    }

    [Test]
    public async Task AttachAsync_rejects_when_date_outside_window(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedSelfTransferScenarioAsync(
            cancellationToken,
            siblingBookingDate: new DateOnly(2026, 6, 15)
        );

        var attach = await fixture.AttachService.AttachAsync(
            fixture.SiblingBtId,
            fixture.SelfTransferJeId,
            cancellationToken
        );

        await Assert.That(attach.IsFailure).IsTrue();
        await Assert.That(attach.Error).IsTypeOf<InvariantError>();
    }

    [Test]
    public async Task AttachAsync_rejects_when_amount_mismatch(CancellationToken cancellationToken)
    {
        // The seeded scenario produces a savings line with Amount = 25000 (the counter-side of
        // a -25000 source). Sibling BT must have Amount 25000 to match. Use 9999 instead.
        await using var fixture = await SeedSelfTransferScenarioAsync(
            cancellationToken,
            siblingAmount: 9999
        );

        var attach = await fixture.AttachService.AttachAsync(
            fixture.SiblingBtId,
            fixture.SelfTransferJeId,
            cancellationToken
        );

        await Assert.That(attach.IsFailure).IsTrue();
        await Assert.That(attach.Error).IsTypeOf<InvariantError>();
    }

    [Test]
    public async Task AttachAsync_rejects_when_counter_iban_not_on_je(
        CancellationToken cancellationToken
    )
    {
        // Sibling has a non-matching CounterpartyAccountNumber.
        await using var fixture = await SeedSelfTransferScenarioAsync(
            cancellationToken,
            siblingCounterpartyAccountNumber: "NL00WRONG1234567890"
        );

        var attach = await fixture.AttachService.AttachAsync(
            fixture.SiblingBtId,
            fixture.SelfTransferJeId,
            cancellationToken
        );

        await Assert.That(attach.IsFailure).IsTrue();
        await Assert.That(attach.Error).IsTypeOf<InvariantError>();
    }

    [Test]
    public async Task DetachAsync_clears_link_and_flips_line_back_to_uncleared(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedSelfTransferScenarioAsync(cancellationToken);

        var attach = await fixture.AttachService.AttachAsync(
            fixture.SiblingBtId,
            fixture.SelfTransferJeId,
            cancellationToken
        );
        await Assert.That(attach.IsSuccess).IsTrue();

        var detach = await fixture.AttachService.DetachAsync(
            fixture.SiblingBtId,
            cancellationToken
        );

        await Assert.That(detach.IsSuccess).IsTrue();
        var entry = detach.Value!;
        await Assert.That(entry.BankTransactions).Count().IsEqualTo(1);
        // One line is Cleared (originally categorised side), the other should be Uncleared again.
        await Assert
            .That(entry.Lines.Count(l => l.ReconciliationStatus == ReconciliationStatus.Uncleared))
            .IsEqualTo(1);

        using var verifyScope = Factory.Services.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var sibling = await dbContext.BankTransactions.SingleAsync(
            b => b.Id == fixture.SiblingBtId,
            cancellationToken
        );
        await Assert.That(sibling.JournalEntryId).IsNull();
    }

    [Test]
    public async Task DetachAsync_rejects_when_bt_not_attached(CancellationToken cancellationToken)
    {
        await using var fixture = await SeedSelfTransferScenarioAsync(cancellationToken);

        // The sibling BT is unattached by default; detach should fail.
        var detach = await fixture.AttachService.DetachAsync(
            fixture.SiblingBtId,
            cancellationToken
        );

        await Assert.That(detach.IsFailure).IsTrue();
        await Assert.That(detach.Error).IsTypeOf<InvariantError>();
        var invariant = (InvariantError)detach.Error!;
        await Assert.That(invariant.Code).IsEqualTo(ErrorCodes.BankTransactionNotAttached);
    }

    [Test]
    public async Task ComputeHintAsync_returns_unique_match_inside_window(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedSelfTransferScenarioAsync(cancellationToken);

        var hint = await fixture.AttachService.ComputeHintAsync(
            fixture.SiblingBtId,
            cancellationToken
        );

        await Assert.That(hint).IsNotNull();
        await Assert.That(hint!.Id).IsEqualTo(fixture.SelfTransferJeId);
    }

    [Test]
    public async Task ComputeHintAsync_returns_null_when_outside_3_day_window(
        CancellationToken cancellationToken
    )
    {
        // 10 days off — outside the strict 3-day hint window.
        await using var fixture = await SeedSelfTransferScenarioAsync(
            cancellationToken,
            siblingBookingDate: new DateOnly(2026, 5, 27)
        );

        var hint = await fixture.AttachService.ComputeHintAsync(
            fixture.SiblingBtId,
            cancellationToken
        );

        await Assert.That(hint).IsNull();
    }

    [Test]
    public async Task ComputeHintAsync_returns_null_when_multiple_match(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedSelfTransferScenarioAsync(cancellationToken);

        // Categorise an additional BT on the current-account as a second self-transfer to
        // the same savings account at the same amount — this creates a second JE that the
        // sibling BT would also satisfy the predicate against.
        var secondaryBt = await fixture.CreateOwnBtAsync(
            fixture.CurrentAccountBankAccountId,
            amount: -25000,
            bookingDate: new DateOnly(2026, 5, 16),
            counterpartyAccountNumber: fixture.SavingsIban,
            cancellationToken
        );
        var secondJe = await fixture.CategorisationService.CategorizeAsync(
            secondaryBt.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: null,
                NewCounterparty: null,
                Date: new DateOnly(2026, 5, 16),
                Description: "second transfer",
                Lines:
                [
                    new CategorizeBankTransactionLineInput(fixture.SavingsAccountId, 25000, null),
                ]
            ),
            cancellationToken
        );
        await Assert.That(secondJe.IsSuccess).IsTrue();

        var hint = await fixture.AttachService.ComputeHintAsync(
            fixture.SiblingBtId,
            cancellationToken
        );

        await Assert.That(hint).IsNull();
    }

    [Test]
    public async Task ListCandidatesAsync_returns_structural_matches_in_widened_window(
        CancellationToken cancellationToken
    )
    {
        // The strict predicate misses (window=14 days, sibling 7 days off), but a relaxed
        // window should still surface the match. Structural conditions still apply.
        await using var fixture = await SeedSelfTransferScenarioAsync(
            cancellationToken,
            siblingBookingDate: new DateOnly(2026, 5, 24)
        );

        var candidates = await fixture.AttachService.ListCandidatesAsync(
            fixture.SiblingBtId,
            dateWindowDays: 14,
            cancellationToken
        );

        await Assert.That(candidates.IsSuccess).IsTrue();
        await Assert.That(candidates.Value!.Any(c => c.Id == fixture.SelfTransferJeId)).IsTrue();
    }

    [Test]
    public async Task ListCandidatesAsync_drops_strict_counter_iban_match(
        CancellationToken cancellationToken
    )
    {
        // Sibling has a bogus CounterpartyAccountNumber so the strict predicate misses,
        // but the manual picker should still surface the JE because the structural
        // own-Account / currency / amount slot match.
        await using var fixture = await SeedSelfTransferScenarioAsync(
            cancellationToken,
            siblingCounterpartyAccountNumber: "NL00WRONG1234567890"
        );

        var candidates = await fixture.AttachService.ListCandidatesAsync(
            fixture.SiblingBtId,
            dateWindowDays: 14,
            cancellationToken
        );

        await Assert.That(candidates.IsSuccess).IsTrue();
        await Assert.That(candidates.Value!.Any(c => c.Id == fixture.SelfTransferJeId)).IsTrue();
    }

    [Test]
    public async Task ListCandidatesAsync_rejects_negative_window(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedSelfTransferScenarioAsync(cancellationToken);

        var result = await fixture.AttachService.ListCandidatesAsync(
            fixture.SiblingBtId,
            dateWindowDays: -1,
            cancellationToken
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvariantError>();
    }

    /// <summary>
    /// Seeds an own-side self-transfer:
    ///  * Current-account own-Account + BankAccount (NL01 IBAN)
    ///  * Savings own-Account + BankAccount (NL02 IBAN)
    ///  * BT-A on the current-account with CounterpartyAccountNumber = savings IBAN,
    ///    Amount = -25000, categorised as a self-transfer to savings (savings line Uncleared)
    ///  * BT-B on the savings account with CounterpartyAccountNumber = current IBAN,
    ///    Amount = 25000 (positive — money in to savings), unattached
    /// </summary>
    private async Task<Fixture> SeedSelfTransferScenarioAsync(
        CancellationToken cancellationToken,
        DateOnly? siblingBookingDate = null,
        long? siblingAmount = null,
        string? siblingCounterpartyAccountNumber = null
    )
    {
        var scope = Factory.Services.CreateAsyncScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var bankAccountService = scope.ServiceProvider.GetRequiredService<IBankAccountService>();
        var bankTransactionService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionService>();
        var categorisationService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionCategorisationService>();
        var attachService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionAttachService>();

        var currentAccount = (
            await accountService.CreateAsync(
                $"Checking-attach-{Guid.NewGuid():N}",
                AccountType.Asset,
                new CurrencyCode("EUR"),
                cancellationToken
            )
        ).Value!;

        var savingsAccount = (
            await accountService.CreateAsync(
                $"Savings-attach-{Guid.NewGuid():N}",
                AccountType.Asset,
                new CurrencyCode("EUR"),
                cancellationToken
            )
        ).Value!;

        var currentIban = $"NL01ATCH{NextDigits(10)}";
        var savingsIban = $"NL02ATCH{NextDigits(10)}";

        var currentBank = (
            await bankAccountService.CreateAsync(
                new CreateBankAccountInput(
                    Type: BankAccountType.Current,
                    Iban: currentIban,
                    AccountNumber: null,
                    CardIdentifier: null,
                    Bic: null,
                    BankName: null,
                    AccountHolderName: null,
                    CurrencyCode: new CurrencyCode("EUR"),
                    ImporterKey: null,
                    AccountId: currentAccount.Id,
                    CounterpartyId: null
                ),
                cancellationToken
            )
        ).Value!;

        var savingsBank = (
            await bankAccountService.CreateAsync(
                new CreateBankAccountInput(
                    Type: BankAccountType.Current,
                    Iban: savingsIban,
                    AccountNumber: null,
                    CardIdentifier: null,
                    Bic: null,
                    BankName: null,
                    AccountHolderName: null,
                    CurrencyCode: new CurrencyCode("EUR"),
                    ImporterKey: null,
                    AccountId: savingsAccount.Id,
                    CounterpartyId: null
                ),
                cancellationToken
            )
        ).Value!;

        var btA = (
            await bankTransactionService.CreateAsync(
                new CreateBankTransactionInput(
                    BankAccountId: currentBank.Id,
                    BookingDate: new DateOnly(2026, 5, 17),
                    Amount: -25000,
                    CurrencyCode: new CurrencyCode("EUR"),
                    Description: "Transfer to savings",
                    CounterpartyName: null,
                    CounterpartyAccountNumber: savingsIban
                ),
                cancellationToken
            )
        ).Value!;

        var jeResult = await categorisationService.CategorizeAsync(
            btA.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: null,
                NewCounterparty: null,
                Date: new DateOnly(2026, 5, 17),
                Description: "Self-transfer",
                Lines: [new CategorizeBankTransactionLineInput(savingsAccount.Id, 25000, null)]
            ),
            cancellationToken
        );
        await Assert.That(jeResult.IsSuccess).IsTrue();

        var btB = (
            await bankTransactionService.CreateAsync(
                new CreateBankTransactionInput(
                    BankAccountId: savingsBank.Id,
                    BookingDate: siblingBookingDate ?? new DateOnly(2026, 5, 18),
                    Amount: siblingAmount ?? 25000,
                    CurrencyCode: new CurrencyCode("EUR"),
                    Description: "Inbound from current",
                    CounterpartyName: null,
                    CounterpartyAccountNumber: siblingCounterpartyAccountNumber ?? currentIban
                ),
                cancellationToken
            )
        ).Value!;

        return new Fixture(
            scope,
            bankTransactionService,
            categorisationService,
            attachService,
            currentBank.Id,
            savingsBank.Id,
            savingsAccount.Id,
            savingsIban,
            currentIban,
            jeResult.Value!.Id,
            btB.Id
        );
    }

    private sealed record Fixture(
        AsyncServiceScope Scope,
        IBankTransactionService BankTransactionService,
        IBankTransactionCategorisationService CategorisationService,
        IBankTransactionAttachService AttachService,
        BankAccountId CurrentAccountBankAccountId,
        BankAccountId SavingsAccountBankAccountId,
        AccountId SavingsAccountId,
        string SavingsIban,
        string CurrentIban,
        JournalEntryId SelfTransferJeId,
        BankTransactionId SiblingBtId
    ) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Scope.DisposeAsync();

        public async Task<BankTransactionOutput> CreateOwnBtAsync(
            BankAccountId bankAccountId,
            long amount,
            DateOnly bookingDate,
            string? counterpartyAccountNumber,
            CancellationToken cancellationToken
        )
        {
            var result = await BankTransactionService.CreateAsync(
                new CreateBankTransactionInput(
                    BankAccountId: bankAccountId,
                    BookingDate: bookingDate,
                    Amount: amount,
                    CurrencyCode: new CurrencyCode("EUR"),
                    Description: $"helper-{Guid.NewGuid():N}",
                    CounterpartyName: null,
                    CounterpartyAccountNumber: counterpartyAccountNumber
                ),
                cancellationToken
            );
            await Assert.That(result.IsSuccess).IsTrue();
            return result.Value!;
        }
    }

    private static string NextDigits(int length)
    {
        var digits = new char[length];
        for (var i = 0; i < length; i++)
            digits[i] = (char)(
                '0' + System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 10)
            );
        return new string(digits);
    }
}
