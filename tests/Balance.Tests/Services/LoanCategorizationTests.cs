using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

internal sealed class LoanCategorizationTests : EndpointsTestsBase
{
    private static readonly CurrencyCode Eur = new("EUR");

    [Test]
    public async Task Loan_aware_categorize_posts_attributed_payment_with_correct_entry_shape(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var loan = await fixture.CreateLoanAsync(parts: 2, cancellationToken);
        var partA = loan.Parts[0];

        var bankTransaction = await fixture.CreateBankTransactionAsync(
            amount: -120_000,
            counterpartyAccountNumber: Fixture.LenderIban,
            cancellationToken
        );

        // Scoped to a subset of parts: only part A's principal and interest are posted.
        var result = await fixture.CategorizationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: fixture.LenderId,
                NewCounterparty: null,
                Date: bankTransaction.BookingDate,
                Description: "Monthly payment",
                Lines:
                [
                    new CategorizeBankTransactionLineInput(
                        partA.AccountId,
                        90_000,
                        "principal",
                        partA.Id
                    ),
                    new CategorizeBankTransactionLineInput(
                        loan.InterestExpenseAccountId,
                        30_000,
                        "interest",
                        partA.Id
                    ),
                ]
            ),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var entry = result.Value!;
        await Assert.That(entry.Lines).Count().IsEqualTo(3);
        await Assert.That(entry.Lines.Sum(l => l.Amount)).IsEqualTo(0L);

        // ADR-0013 shape: bank side Cleared, counter side Uncleared.
        var bankLine = entry.Lines.Single(l => l.AccountId == fixture.CheckingAccountId);
        await Assert.That(bankLine.ReconciliationStatus).IsEqualTo(ReconciliationStatus.Cleared);
        var counterLines = entry.Lines.Where(l => l.AccountId != fixture.CheckingAccountId);
        await Assert
            .That(counterLines.All(l => l.ReconciliationStatus == ReconciliationStatus.Uncleared))
            .IsTrue();

        // Attribution persisted on both loan lines; part B untouched.
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var attributed = await dbContext
            .JournalLines.Where(l => l.LoanPartId == partA.Id)
            .CountAsync(cancellationToken);
        await Assert.That(attributed).IsEqualTo(2);
        var partBLines = await dbContext
            .JournalLines.Where(l => l.LoanPartId == loan.Parts[1].Id)
            .CountAsync(cancellationToken);
        await Assert.That(partBLines).IsEqualTo(0);
    }

    [Test]
    public async Task Plain_categorize_refuses_loan_managed_target(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var loan = await fixture.CreateLoanAsync(parts: 1, cancellationToken);

        var bankTransaction = await fixture.CreateBankTransactionAsync(
            amount: -120_000,
            counterpartyAccountNumber: null,
            cancellationToken
        );

        var result = await fixture.CategorizationService.CategorizeAsync(
            bankTransaction.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: fixture.LenderId,
                NewCounterparty: null,
                Date: bankTransaction.BookingDate,
                Description: "Plain categorize onto a part account",
                Lines:
                [
                    new CategorizeBankTransactionLineInput(loan.Parts[0].AccountId, 120_000, null),
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
    public async Task Payment_proposal_prefills_engine_numbers_for_the_current_month(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        // Interest-only at 2.4% on €100,000 → €200.00 interest, no principal.
        var loan = await fixture.CreateLoanAsync(
            parts: 1,
            cancellationToken,
            repaymentType: LoanRepaymentType.InterestOnly,
            openingBalance: 10_000_000,
            annualRatePercent: 2.4m
        );

        var month = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await fixture.ProjectionService.GetPaymentProposalAsync(
            loan.Id,
            month,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var proposal = result.Value!;
        await Assert.That(proposal.Lines).Count().IsEqualTo(1);
        await Assert.That(proposal.Lines[0].Interest).IsEqualTo(20_000L);
        await Assert.That(proposal.Lines[0].Principal).IsEqualTo(0L);
        await Assert.That(proposal.Total).IsEqualTo(20_000L);
        await Assert
            .That(proposal.InterestExpenseAccountId)
            .IsEqualTo(loan.InterestExpenseAccountId);
    }

    [Test]
    public async Task Inbox_rows_from_the_lender_surface_a_loan_payment_hint(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var loan = await fixture.CreateLoanAsync(parts: 1, cancellationToken);

        await fixture.CreateBankTransactionAsync(
            amount: -120_000,
            counterpartyAccountNumber: Fixture.LenderIban,
            cancellationToken
        );

        var page = await fixture.BankTransactionService.ListAsync(
            0,
            50,
            BankTransactionListFilter.Inbox,
            search: null,
            cancellationToken
        );

        var row = page.Items.Single(r => r.CounterpartyAccountNumber == Fixture.LenderIban);
        await Assert.That(row.LoanPaymentHint).IsNotNull();
        await Assert.That(row.LoanPaymentHint!.LoanId).IsEqualTo(loan.Id);
        await Assert.That(row.LoanPaymentHint.LoanName).IsEqualTo(loan.Name);
    }

    [Test]
    public async Task Projection_returns_actuals_baseline_and_scenario_totals(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var loan = await fixture.CreateLoanAsync(parts: 1, cancellationToken);
        var part = loan.Parts[0];

        var result = await fixture.ProjectionService.GetProjectionAsync(
            loan.Id,
            new LoanScenarioInput(
                [
                    new LoanScenarioExtraRepaymentInput(
                        part.Id,
                        DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(2),
                        1_000_000
                    ),
                ],
                Balance.Services.Loans.ExtraRepaymentPolicy.LowerPayment,
                AssumedAnnualRatePercent: null
            ),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var projection = result.Value!;

        // Actuals start where the ledger starts: the opening-balance month.
        await Assert.That(projection.Actuals.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(projection.Actuals[^1].EndBalance).IsEqualTo(10_000_000L);

        await Assert.That(projection.Baseline.Count).IsGreaterThan(0);
        await Assert.That(projection.Scenario).IsNotNull();
        await Assert.That(projection.Totals).IsNotNull();
        await Assert.That(projection.Totals!.InterestSaved).IsGreaterThan(0L);
        await Assert.That(projection.Totals.NextPaymentDelta).IsLessThan(0L);
        await Assert.That(projection.Parts).Count().IsEqualTo(1);
    }

    private async Task<Fixture> SeedAsync(CancellationToken cancellationToken)
    {
        var scope = Factory.Services.CreateAsyncScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var bankAccountService = scope.ServiceProvider.GetRequiredService<IBankAccountService>();
        var bankTransactionService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionService>();
        var counterpartyService = scope.ServiceProvider.GetRequiredService<ICounterpartyService>();
        var categorizationService =
            scope.ServiceProvider.GetRequiredService<IBankTransactionCategorizationService>();
        var loanService = scope.ServiceProvider.GetRequiredService<ILoanService>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ILoanProjectionService>();

        var checking = (
            await accountService.CreateAsync(
                $"Checking-loan-{Guid.NewGuid():N}",
                AccountType.Asset,
                Eur,
                cancellationToken
            )
        ).Value!;
        var checkingBank = (
            await bankAccountService.CreateAsync(
                new CreateBankAccountInput(
                    Type: BankAccountType.Current,
                    Iban: $"NL69INGB{NextDigits(10)}",
                    AccountNumber: null,
                    CardIdentifier: null,
                    Bic: null,
                    BankName: null,
                    AccountHolderName: null,
                    CurrencyCode: Eur,
                    ImporterKey: null,
                    AccountId: checking.Id,
                    CounterpartyId: null
                ),
                cancellationToken
            )
        ).Value!;

        var lender = (
            await counterpartyService.CreateAsync($"Big-Bank-{Guid.NewGuid():N}", cancellationToken)
        ).Value!;

        // The lender's own bank account: what loan-payment debits reference as the counterparty
        // account number, and what the Inbox hint resolves through (ADR-0025).
        var lenderBank = await bankAccountService.CreateAsync(
            new CreateBankAccountInput(
                Type: BankAccountType.Current,
                Iban: Fixture.LenderIban,
                AccountNumber: null,
                CardIdentifier: null,
                Bic: null,
                BankName: null,
                AccountHolderName: null,
                CurrencyCode: null,
                ImporterKey: null,
                AccountId: null,
                CounterpartyId: lender.Id
            ),
            cancellationToken
        );
        if (lenderBank.IsFailure)
            throw new InvalidOperationException(lenderBank.Error!.ToString());

        var interest = (
            await accountService.CreateAsync(
                $"Loan-Interest-{Guid.NewGuid():N}",
                AccountType.Expense,
                Eur,
                cancellationToken
            )
        ).Value!;

        return new Fixture(
            scope,
            bankTransactionService,
            categorizationService,
            loanService,
            projectionService,
            checking.Id,
            checkingBank.Id,
            lender.Id,
            interest.Id
        );
    }

    private static string NextDigits(int count)
    {
        var guid = Guid.NewGuid().ToString("N");
        var digits = new char[count];
        for (var i = 0; i < count; i++)
            digits[i] = (char)('0' + (guid[i] % 10));
        return new string(digits);
    }

    private sealed record Fixture(
        AsyncServiceScope Scope,
        IBankTransactionService BankTransactionService,
        IBankTransactionCategorizationService CategorizationService,
        ILoanService LoanService,
        ILoanProjectionService ProjectionService,
        AccountId CheckingAccountId,
        BankAccountId CheckingBankAccountId,
        CounterpartyId LenderId,
        AccountId InterestAccountId
    ) : IAsyncDisposable
    {
        public const string LenderIban = "NL77BIGB0001234567";

        public ValueTask DisposeAsync() => Scope.DisposeAsync();

        public async Task<LoanDetailOutput> CreateLoanAsync(
            int parts,
            CancellationToken cancellationToken,
            LoanRepaymentType repaymentType = LoanRepaymentType.Annuity,
            long openingBalance = 10_000_000,
            decimal annualRatePercent = 3.6m
        )
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var partInputs = Enumerable
                .Range(1, parts)
                .Select(i => new CreateLoanPartInput(
                    $"Part {i}",
                    repaymentType,
                    today.AddYears(-1),
                    today.AddYears(29),
                    AdoptAccountId: null,
                    NewAccount: new NewLoanPartAccountInput(
                        $"Part-{i}-{Guid.NewGuid():N}"[..24],
                        $"L{Guid.NewGuid():N}"[..16],
                        openingBalance,
                        today.AddYears(-1)
                    ),
                    RatePeriods:
                    [
                        new CreateLoanRatePeriodInput(today.AddYears(-1), annualRatePercent, null),
                    ]
                ))
                .ToList();

            var result = await LoanService.CreateAsync(
                new CreateLoanInput(
                    $"Loan-{Guid.NewGuid():N}"[..20],
                    LenderId,
                    InterestAccountId,
                    Eur,
                    $"Loan-Parent-{Guid.NewGuid():N}"[..24],
                    $"P{Guid.NewGuid():N}"[..16],
                    partInputs
                ),
                cancellationToken
            );
            if (result.IsFailure)
                throw new InvalidOperationException(result.Error!.ToString());

            return result.Value!;
        }

        public async Task<BankTransactionOutput> CreateBankTransactionAsync(
            long amount,
            string? counterpartyAccountNumber,
            CancellationToken cancellationToken
        )
        {
            var result = await BankTransactionService.CreateAsync(
                new CreateBankTransactionInput(
                    BankAccountId: CheckingBankAccountId,
                    BookingDate: DateOnly.FromDateTime(DateTime.UtcNow),
                    Amount: amount,
                    CurrencyCode: Eur,
                    Description: "loan-test",
                    CounterpartyName: "Big Bank",
                    CounterpartyAccountNumber: counterpartyAccountNumber
                ),
                cancellationToken
            );
            return result.Value!;
        }
    }
}
