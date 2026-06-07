using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

internal sealed class LoanServiceTests : EndpointsTestsBase
{
    private static readonly CurrencyCode Eur = new("EUR");

    [Test]
    public async Task CreateAsync_with_fresh_part_builds_subtree_and_opening_balance(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var result = await fixture.LoanService.CreateAsync(
            fixture.LoanInput(
                FreshPart("Part 1", LoanRepaymentType.Annuity, openingBalance: 25_000_000)
            ),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var loan = result.Value!;
        await Assert.That(loan.Parts).Count().IsEqualTo(1);
        await Assert.That(loan.OutstandingBalance).IsEqualTo(25_000_000L);
        await Assert.That(loan.Parts[0].OutstandingBalance).IsEqualTo(25_000_000L);
        await Assert.That(loan.CurrentPayment).IsGreaterThan(0L);
        await Assert.That(loan.WeightedAnnualRatePercent).IsEqualTo(3.6m);
        await Assert.That(loan.IsEnded).IsFalse();

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();

        var parent = await dbContext.Accounts.SingleAsync(
            a => a.Id == loan.ParentAccountId,
            cancellationToken
        );
        await Assert.That(parent.AccountType).IsEqualTo(AccountType.Liability);
        await Assert.That(parent.IsPostable).IsFalse();
        await Assert.That(parent.IsLiquid).IsFalse();

        var partAccount = await dbContext.Accounts.SingleAsync(
            a => a.Id == loan.Parts[0].AccountId,
            cancellationToken
        );
        await Assert.That(partAccount.ParentAccountId).IsEqualTo(loan.ParentAccountId);
        await Assert.That(partAccount.IsPostable).IsTrue();
        await Assert.That(partAccount.IsLiquid).IsFalse();

        // The opening-balance entry: part account credited, both legs Reconciled.
        var openingLines = await dbContext
            .JournalLines.Where(l => l.AccountId == partAccount.Id)
            .ToListAsync(cancellationToken);
        await Assert.That(openingLines).Count().IsEqualTo(1);
        await Assert.That(openingLines[0].Amount).IsEqualTo(-25_000_000L);
        await Assert
            .That(openingLines[0].ReconciliationStatus)
            .IsEqualTo(ReconciliationStatus.Reconciled);
    }

    [Test]
    public async Task CreateAsync_adopting_existing_liability_keeps_history(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        // An existing mortgage liability with posted history (an opening balance entry).
        var existing = await fixture.CreateAccountAsync(
            "Old-Mortgage",
            AccountType.Liability,
            cancellationToken
        );
        await fixture.PostAsync(existing.Id, -30_000_000, cancellationToken);

        var result = await fixture.LoanService.CreateAsync(
            fixture.LoanInput(AdoptPart("Part 1", existing.Id)),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var loan = result.Value!;
        await Assert.That(loan.OutstandingBalance).IsEqualTo(30_000_000L);
        await Assert.That(loan.Parts[0].AccountId).IsEqualTo(existing.Id);

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var adopted = await dbContext.Accounts.SingleAsync(
            a => a.Id == existing.Id,
            cancellationToken
        );
        await Assert.That(adopted.ParentAccountId).IsEqualTo(loan.ParentAccountId);
        await Assert.That(adopted.IsLiquid).IsFalse();
    }

    [Test]
    public async Task CreateAsync_refuses_adopting_non_liability_or_already_managed_accounts(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var asset = await fixture.CreateAccountAsync(
            "Savings",
            AccountType.Asset,
            cancellationToken
        );
        var assetResult = await fixture.LoanService.CreateAsync(
            fixture.LoanInput(AdoptPart("Part 1", asset.Id)),
            cancellationToken
        );
        await Assert.That(assetResult.IsFailure).IsTrue();
        await Assert
            .That(((InvariantError)assetResult.Error!).Code)
            .IsEqualTo(ErrorCodes.LoanPartAccountInvalid);

        // Adopt a liability into loan #1, then try to adopt the same account into loan #2.
        var liability = await fixture.CreateAccountAsync(
            "Car-Loan",
            AccountType.Liability,
            cancellationToken
        );
        var first = await fixture.LoanService.CreateAsync(
            fixture.LoanInput(AdoptPart("Part 1", liability.Id)),
            cancellationToken
        );
        await Assert.That(first.IsSuccess).IsTrue();

        var second = await fixture.LoanService.CreateAsync(
            fixture.LoanInput(AdoptPart("Part 1", liability.Id)),
            cancellationToken
        );
        await Assert.That(second.IsFailure).IsTrue();
        await Assert
            .That(((InvariantError)second.Error!).Code)
            .IsEqualTo(ErrorCodes.AccountLoanManaged);
    }

    [Test]
    public async Task AddRatePeriodAsync_appends_and_refuses_duplicate_effective_dates(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var loan = (
            await fixture.LoanService.CreateAsync(
                fixture.LoanInput(FreshPart("Part 1", LoanRepaymentType.Annuity, 10_000_000)),
                cancellationToken
            )
        ).Value!;
        var part = loan.Parts[0];

        var appended = await fixture.LoanService.AddRatePeriodAsync(
            loan.Id,
            part.Id,
            new CreateLoanRatePeriodInput(new DateOnly(2030, 7, 1), 4.2m, null),
            cancellationToken
        );
        await Assert.That(appended.IsSuccess).IsTrue();
        await Assert.That(appended.Value!.Parts[0].RatePeriods).Count().IsEqualTo(2);

        var duplicate = await fixture.LoanService.AddRatePeriodAsync(
            loan.Id,
            part.Id,
            new CreateLoanRatePeriodInput(new DateOnly(2030, 7, 1), 5.0m, null),
            cancellationToken
        );
        await Assert.That(duplicate.IsFailure).IsTrue();
        await Assert
            .That(((ConflictError)duplicate.Error!).Code)
            .IsEqualTo(ErrorCodes.LoanRatePeriodConflict);
    }

    [Test]
    public async Task Manual_entry_refuses_loan_managed_account_without_attribution(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var loan = (
            await fixture.LoanService.CreateAsync(
                fixture.LoanInput(FreshPart("Part 1", LoanRepaymentType.Annuity, 10_000_000)),
                cancellationToken
            )
        ).Value!;
        var checking = await fixture.CreateAccountAsync(
            "Checking",
            AccountType.Asset,
            cancellationToken
        );

        var result = await fixture.JournalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                new DateOnly(2026, 7, 1),
                "Sneaky principal",
                null,
                [
                    new CreateJournalLineInput(checking.Id, -90_000, null),
                    new CreateJournalLineInput(loan.Parts[0].AccountId, 90_000, null),
                ]
            ),
            cancellationToken
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert
            .That(((InvariantError)result.Error!).Code)
            .IsEqualTo(ErrorCodes.AccountLoanManaged);
    }

    [Test]
    public async Task Attributed_lines_post_to_part_and_interest_accounts(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var loan = (
            await fixture.LoanService.CreateAsync(
                fixture.LoanInput(FreshPart("Part 1", LoanRepaymentType.Annuity, 10_000_000)),
                cancellationToken
            )
        ).Value!;
        var part = loan.Parts[0];
        var checking = await fixture.CreateAccountAsync(
            "Checking",
            AccountType.Asset,
            cancellationToken
        );

        var result = await fixture.JournalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                new DateOnly(2026, 7, 1),
                "Loan payment",
                null,
                [
                    new CreateJournalLineInput(checking.Id, -120_000, null),
                    new CreateJournalLineInput(part.AccountId, 90_000, null, LoanPartId: part.Id),
                    new CreateJournalLineInput(
                        loan.InterestExpenseAccountId,
                        30_000,
                        null,
                        LoanPartId: part.Id
                    ),
                ]
            ),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var attributed = await dbContext
            .JournalLines.Where(l => l.LoanPartId == part.Id)
            .CountAsync(cancellationToken);
        await Assert.That(attributed).IsEqualTo(2);

        // The posted principal reduced the outstanding balance — the ledger is the truth.
        var detail = (await fixture.LoanService.GetAsync(loan.Id, cancellationToken)).Value!;
        await Assert.That(detail.OutstandingBalance).IsEqualTo(9_910_000L);
    }

    [Test]
    public async Task Attribution_to_an_unrelated_account_is_refused(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var loan = (
            await fixture.LoanService.CreateAsync(
                fixture.LoanInput(FreshPart("Part 1", LoanRepaymentType.Annuity, 10_000_000)),
                cancellationToken
            )
        ).Value!;
        var checking = await fixture.CreateAccountAsync(
            "Checking",
            AccountType.Asset,
            cancellationToken
        );
        var groceries = await fixture.CreateAccountAsync(
            "Groceries",
            AccountType.Expense,
            cancellationToken
        );

        var result = await fixture.JournalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                new DateOnly(2026, 7, 1),
                "Mis-attributed",
                null,
                [
                    new CreateJournalLineInput(checking.Id, -100, null),
                    new CreateJournalLineInput(
                        groceries.Id,
                        100,
                        null,
                        LoanPartId: loan.Parts[0].Id
                    ),
                ]
            ),
            cancellationToken
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert
            .That(((InvariantError)result.Error!).Code)
            .IsEqualTo(ErrorCodes.LoanPartAttributionInvalid);
    }

    [Test]
    public async Task Reassign_refuses_loan_managed_source_and_target(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var loan = (
            await fixture.LoanService.CreateAsync(
                fixture.LoanInput(FreshPart("Part 1", LoanRepaymentType.Annuity, 10_000_000)),
                cancellationToken
            )
        ).Value!;
        var part = loan.Parts[0];
        var checking = await fixture.CreateAccountAsync(
            "Checking",
            AccountType.Asset,
            cancellationToken
        );
        var entry = (
            await fixture.JournalEntryService.CreateAsync(
                new CreateJournalEntryInput(
                    new DateOnly(2026, 7, 1),
                    "Extra repayment",
                    null,
                    [
                        new CreateJournalLineInput(checking.Id, -50_000, null),
                        new CreateJournalLineInput(
                            part.AccountId,
                            50_000,
                            null,
                            LoanPartId: part.Id
                        ),
                    ]
                ),
                cancellationToken
            )
        ).Value!;

        // Moving the principal line off the part account is refused...
        var principalLine = entry.Lines.Single(l => l.AccountId == part.AccountId);
        var moveOff = await fixture.JournalEntryService.ReassignLinesAsync(
            [principalLine.Id],
            checking.Id,
            cancellationToken
        );
        await Assert.That(moveOff.IsFailure).IsTrue();
        await Assert
            .That(((InvariantError)moveOff.Error!).Code)
            .IsEqualTo(ErrorCodes.AccountLoanManaged);

        // ...and so is moving any line onto it.
        var checkingLine = entry.Lines.Single(l => l.AccountId == checking.Id);
        var moveOnto = await fixture.JournalEntryService.ReassignLinesAsync(
            [checkingLine.Id],
            part.AccountId,
            cancellationToken
        );
        await Assert.That(moveOnto.IsFailure).IsTrue();
        await Assert
            .That(((InvariantError)moveOnto.Error!).Code)
            .IsEqualTo(ErrorCodes.AccountLoanManaged);
    }

    [Test]
    public async Task Generic_edit_freezes_lines_on_loan_managed_accounts(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var loan = (
            await fixture.LoanService.CreateAsync(
                fixture.LoanInput(FreshPart("Part 1", LoanRepaymentType.Annuity, 10_000_000)),
                cancellationToken
            )
        ).Value!;
        var part = loan.Parts[0];
        var checking = await fixture.CreateAccountAsync(
            "Checking",
            AccountType.Asset,
            cancellationToken
        );
        var entry = (
            await fixture.JournalEntryService.CreateAsync(
                new CreateJournalEntryInput(
                    new DateOnly(2026, 7, 1),
                    "Extra repayment",
                    null,
                    [
                        new CreateJournalLineInput(checking.Id, -50_000, null),
                        new CreateJournalLineInput(
                            part.AccountId,
                            50_000,
                            null,
                            LoanPartId: part.Id
                        ),
                    ]
                ),
                cancellationToken
            )
        ).Value!;
        var principalLine = entry.Lines.Single(l => l.AccountId == part.AccountId);
        var checkingLine = entry.Lines.Single(l => l.AccountId == checking.Id);

        // Changing the principal line's amount through the generic PUT is refused.
        var amountEdit = await fixture.JournalEntryService.ReplaceAsync(
            entry.Id,
            new ReplaceJournalEntryInput(
                entry.Date,
                entry.Description,
                null,
                [
                    new ReplaceJournalLineInput(checkingLine.Id, checking.Id, -49_000, null),
                    new ReplaceJournalLineInput(principalLine.Id, part.AccountId, 49_000, null),
                ]
            ),
            cancellationToken
        );
        await Assert.That(amountEdit.IsFailure).IsTrue();
        await Assert
            .That(((InvariantError)amountEdit.Error!).Code)
            .IsEqualTo(ErrorCodes.AccountLoanManaged);

        // A description-only edit on the frozen line is fine (mirrors ADR-0014).
        var descriptionEdit = await fixture.JournalEntryService.ReplaceAsync(
            entry.Id,
            new ReplaceJournalEntryInput(
                entry.Date,
                entry.Description,
                null,
                [
                    new ReplaceJournalLineInput(checkingLine.Id, checking.Id, -50_000, null),
                    new ReplaceJournalLineInput(
                        principalLine.Id,
                        part.AccountId,
                        50_000,
                        "principal — corrected note"
                    ),
                ]
            ),
            cancellationToken
        );
        await Assert.That(descriptionEdit.IsSuccess).IsTrue();
    }

    [Test]
    public async Task DeleteAsync_keeps_accounts_and_history(CancellationToken cancellationToken)
    {
        await using var fixture = await SeedAsync(cancellationToken);

        var loan = (
            await fixture.LoanService.CreateAsync(
                fixture.LoanInput(FreshPart("Part 1", LoanRepaymentType.Annuity, 10_000_000)),
                cancellationToken
            )
        ).Value!;
        var partAccountId = loan.Parts[0].AccountId;

        var deleted = await fixture.LoanService.DeleteAsync(loan.Id, cancellationToken);
        await Assert.That(deleted.IsSuccess).IsTrue();

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        await Assert
            .That(await dbContext.Accounts.AnyAsync(a => a.Id == partAccountId, cancellationToken))
            .IsTrue();
        await Assert
            .That(
                await dbContext.JournalLines.AnyAsync(
                    l => l.AccountId == partAccountId,
                    cancellationToken
                )
            )
            .IsTrue();
        await Assert
            .That(await dbContext.LoanParts.AnyAsync(cancellationToken: cancellationToken))
            .IsFalse();
    }

    private static CreateLoanPartInput FreshPart(
        string label,
        LoanRepaymentType type,
        long openingBalance
    ) =>
        new(
            label,
            type,
            new DateOnly(2026, 1, 1),
            new DateOnly(2056, 1, 1),
            AdoptAccountId: null,
            NewAccount: new NewLoanPartAccountInput(
                $"{label}-{Guid.NewGuid():N}"[..24],
                $"L{Guid.NewGuid():N}"[..16],
                openingBalance,
                new DateOnly(2026, 1, 1)
            ),
            RatePeriods: [new CreateLoanRatePeriodInput(new DateOnly(2026, 1, 1), 3.6m, null)]
        );

    private static CreateLoanPartInput AdoptPart(string label, AccountId accountId) =>
        new(
            label,
            LoanRepaymentType.Annuity,
            new DateOnly(2026, 1, 1),
            new DateOnly(2056, 1, 1),
            AdoptAccountId: accountId,
            NewAccount: null,
            RatePeriods: [new CreateLoanRatePeriodInput(new DateOnly(2026, 1, 1), 3.6m, null)]
        );

    private async Task<Fixture> SeedAsync(CancellationToken cancellationToken)
    {
        var scope = Factory.Services.CreateAsyncScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var counterpartyService = scope.ServiceProvider.GetRequiredService<ICounterpartyService>();
        var loanService = scope.ServiceProvider.GetRequiredService<ILoanService>();
        var journalEntryService = scope.ServiceProvider.GetRequiredService<IJournalEntryService>();

        var lender = (
            await counterpartyService.CreateAsync($"Big-Bank-{Guid.NewGuid():N}", cancellationToken)
        ).Value!;
        var interest = (
            await accountService.CreateAsync(
                $"Mortgage-Interest-{Guid.NewGuid():N}",
                AccountType.Expense,
                Eur,
                cancellationToken
            )
        ).Value!;

        return new Fixture(
            scope,
            accountService,
            loanService,
            journalEntryService,
            lender.Id,
            interest.Id
        );
    }

    private sealed record Fixture(
        AsyncServiceScope Scope,
        IAccountService AccountService,
        ILoanService LoanService,
        IJournalEntryService JournalEntryService,
        CounterpartyId LenderId,
        AccountId InterestAccountId
    ) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Scope.DisposeAsync();

        public CreateLoanInput LoanInput(params CreateLoanPartInput[] parts) =>
            new(
                $"Loan-{Guid.NewGuid():N}"[..20],
                LenderId,
                InterestAccountId,
                Eur,
                $"Loan-Parent-{Guid.NewGuid():N}"[..24],
                $"P{Guid.NewGuid():N}"[..16],
                parts
            );

        public async Task<AccountOutput> CreateAccountAsync(
            string namePrefix,
            AccountType type,
            CancellationToken cancellationToken
        )
        {
            var result = await AccountService.CreateAsync(
                $"{namePrefix}-{Guid.NewGuid():N}",
                type,
                Eur,
                cancellationToken
            );
            return result.Value!;
        }

        /// <summary>Posts an opening-style balance onto an account via the Opening Balances leg.</summary>
        public async Task PostAsync(
            AccountId accountId,
            long amount,
            CancellationToken cancellationToken
        )
        {
            var result = await JournalEntryService.CreateAsync(
                new CreateJournalEntryInput(
                    new DateOnly(2026, 1, 1),
                    "Opening balance",
                    null,
                    [
                        new CreateJournalLineInput(accountId, amount, null),
                        new CreateJournalLineInput(
                            Balance.Data.Configurations.AccountSeed.OpeningBalancesId,
                            -amount,
                            null
                        ),
                    ]
                ),
                cancellationToken
            );
            if (result.IsFailure)
                throw new InvalidOperationException(result.Error!.ToString());
        }
    }
}
