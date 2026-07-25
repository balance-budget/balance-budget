using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

// Behavior of the construction-deposit model (ADR-0037): the payment proposal's Deposit
// settlement leg, the Deposit-interest credit landing in the balance, generalized Attach for the
// Verrekening row, and the net-during-construction / gross-at-zero headline.
internal sealed class ConstructionDepositTests : EndpointsTestsBase
{
    private static readonly CurrencyCode Eur = new("EUR");

    private const long DepositOpeningBalance = 1_000_000; // 10,000.00
    private const decimal DepositRate = 2.4m; // → 2,000 minor units per month
    private const long MonthlySettlement = 2_000;
    private const long PartOpeningBalance = 10_000_000; // 100,000.00
    private const decimal PartRate = 3.6m; // interest-only → 30,000 minor units per month
    private const long MonthlyInterest = 30_000;

    [Test]
    public async Task Proposal_settlement_reads_the_posted_prior_month_credit(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var loan = await fixture.CreateLoanWithDepositAsync(
            DepositOpeningBalance,
            cancellationToken
        );

        var month = FirstOfMonth(DateOnly.FromDateTime(DateTime.UtcNow));
        // Post a prior-month credit that differs from the balance × rate fallback so we can tell
        // the read-through from the estimate.
        await fixture.PostInterestCreditAsync(
            2_500,
            month.AddMonths(-1).AddDays(14),
            cancellationToken
        );

        var result = await fixture.ProjectionService.GetPaymentProposalAsync(
            loan.Id,
            month,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var settlement = result.Value!.DepositSettlement;
        await Assert.That(settlement).IsNotNull();
        await Assert.That(settlement!.Amount).IsEqualTo(2_500L);
        await Assert.That(settlement.DepositAccountId).IsEqualTo(fixture.DepositAccountId);
    }

    [Test]
    public async Task Proposal_settlement_falls_back_to_balance_times_rate(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var loan = await fixture.CreateLoanWithDepositAsync(
            DepositOpeningBalance,
            cancellationToken
        );

        var month = FirstOfMonth(DateOnly.FromDateTime(DateTime.UtcNow));
        var result = await fixture.ProjectionService.GetPaymentProposalAsync(
            loan.Id,
            month,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DepositSettlement!.Amount).IsEqualTo(MonthlySettlement);
    }

    [Test]
    public async Task Proposal_settlement_is_null_in_the_first_month_with_no_balance(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var loan = await fixture.CreateLoanWithDepositAsync(openingBalance: 0, cancellationToken);

        var month = FirstOfMonth(DateOnly.FromDateTime(DateTime.UtcNow));
        var result = await fixture.ProjectionService.GetPaymentProposalAsync(
            loan.Id,
            month,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value!.DepositSettlement).IsNull();
    }

    [Test]
    public async Task Categorizing_the_checking_debit_posts_an_uncleared_deposit_settlement_leg(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var loan = await fixture.CreateLoanWithDepositAsync(
            DepositOpeningBalance,
            cancellationToken
        );
        var part = loan.Parts[0];

        var netDebit = MonthlyInterest - MonthlySettlement; // gross − settlement
        var bt = await fixture.CreateCheckingDebitAsync(-netDebit, cancellationToken);

        var result = await fixture.CategorizationService.CategorizeAsync(
            bt.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: fixture.LenderId,
                NewCounterparty: null,
                Date: bt.BookingDate,
                Description: "Monthly payment",
                Lines:
                [
                    new CategorizeBankTransactionLineInput(
                        fixture.InterestAccountId,
                        MonthlyInterest,
                        "interest",
                        part.Id
                    ),
                    new CategorizeBankTransactionLineInput(
                        fixture.DepositAccountId,
                        -MonthlySettlement,
                        "deposit settlement"
                    ),
                ]
            ),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var entry = result.Value!;
        await Assert.That(entry.Lines.Sum(l => l.Amount)).IsEqualTo(0L);

        var cashLine = entry.Lines.Single(l => l.AccountId == fixture.CheckingAccountId);
        await Assert.That(cashLine.Amount).IsEqualTo(-netDebit);
        await Assert.That(cashLine.ReconciliationStatus).IsEqualTo(ReconciliationStatus.Cleared);

        var depositLine = entry.Lines.Single(l => l.AccountId == fixture.DepositAccountId);
        await Assert.That(depositLine.Amount).IsEqualTo(-MonthlySettlement);
        await Assert
            .That(depositLine.ReconciliationStatus)
            .IsEqualTo(ReconciliationStatus.Uncleared);

        // The settlement leg carries no Loan Part attribution (a loan-level line).
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var settlementLoanPart = await dbContext
            .JournalLines.Where(l =>
                l.JournalEntryId == entry.Id && l.AccountId == fixture.DepositAccountId
            )
            .Select(l => l.LoanPartId)
            .SingleAsync(cancellationToken);
        await Assert.That(settlementLoanPart).IsNull();
    }

    [Test]
    public async Task Categorizing_the_interest_credit_raises_the_deposit_balance(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        await fixture.CreateLoanWithDepositAsync(DepositOpeningBalance, cancellationToken);

        var credit = await fixture.CreateDepositCreditBtAsync(2_100, cancellationToken);
        var result = await fixture.CategorizationService.CategorizeAsync(
            credit.Id,
            new CategorizeBankTransactionInput(
                CounterpartyId: null,
                NewCounterparty: null,
                Date: credit.BookingDate,
                Description: "Deposit interest",
                Lines:
                [
                    new CategorizeBankTransactionLineInput(fixture.IncomeAccountId, -2_100, null),
                ]
            ),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var balance = await fixture.DepositBalanceAsync(cancellationToken);
        await Assert.That(balance).IsEqualTo(DepositOpeningBalance + 2_100);
    }

    [Test]
    public async Task Verrekening_attaches_to_the_loan_payment_and_flips_the_deposit_leg(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var loan = await fixture.CreateLoanWithDepositAsync(
            DepositOpeningBalance,
            cancellationToken
        );
        var payment = await fixture.PostLoanPaymentAsync(loan, cancellationToken);

        var verrekening = await fixture.CreateDepositDebitBtAsync(
            -MonthlySettlement,
            cancellationToken
        );

        var hint = await fixture.AttachService.ComputeHintAsync(verrekening.Id, cancellationToken);
        await Assert.That(hint).IsNotNull();
        await Assert.That(hint!.Id).IsEqualTo(payment.Id);

        var attach = await fixture.AttachService.AttachAsync(
            verrekening.Id,
            payment.Id,
            cancellationToken
        );
        await Assert.That(attach.IsSuccess).IsTrue();

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var depositLine = await dbContext
            .JournalLines.Where(l =>
                l.JournalEntryId == payment.Id && l.AccountId == fixture.DepositAccountId
            )
            .SingleAsync(cancellationToken);
        await Assert.That(depositLine.ReconciliationStatus).IsEqualTo(ReconciliationStatus.Cleared);

        var referencingBts = await dbContext
            .BankTransactions.Where(b => b.JournalEntryId == payment.Id)
            .CountAsync(cancellationToken);
        await Assert.That(referencingBts).IsEqualTo(2);
    }

    [Test]
    public async Task Non_loan_payment_with_matching_line_still_refuses_the_loose_attach(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        await fixture.CreateLoanWithDepositAsync(DepositOpeningBalance, cancellationToken);

        // A JE shaped like a settlement (a matching Uncleared line on the deposit account) but not
        // a loan payment: it has a Counterparty and no LoanPartId-attributed line.
        var entryId = await fixture.PostNonLoanDepositEntryAsync(
            -MonthlySettlement,
            cancellationToken
        );
        var verrekening = await fixture.CreateDepositDebitBtAsync(
            -MonthlySettlement,
            cancellationToken
        );

        var attach = await fixture.AttachService.AttachAsync(
            verrekening.Id,
            entryId,
            cancellationToken
        );

        await Assert.That(attach.IsFailure).IsTrue();
    }

    [Test]
    public async Task Current_payment_headline_nets_during_construction_and_reverts_to_gross(
        CancellationToken cancellationToken
    )
    {
        await using var fixture = await SeedAsync(cancellationToken);
        var loan = await fixture.CreateLoanWithDepositAsync(
            DepositOpeningBalance,
            cancellationToken
        );

        var funded = await fixture.LoanService.GetAsync(loan.Id, cancellationToken);
        await Assert.That(funded.IsSuccess).IsTrue();
        var netPayment = funded.Value!.CurrentPayment;

        await fixture.DrainDepositAsync(DepositOpeningBalance, cancellationToken);

        var drained = await fixture.LoanService.GetAsync(loan.Id, cancellationToken);
        var grossPayment = drained.Value!.CurrentPayment;

        await Assert.That(grossPayment - netPayment).IsEqualTo(MonthlySettlement);
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private async Task<Fixture> SeedAsync(CancellationToken cancellationToken)
    {
        var scope = Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var accountService = services.GetRequiredService<IAccountService>();
        var bankAccountService = services.GetRequiredService<IBankAccountService>();
        var counterpartyService = services.GetRequiredService<ICounterpartyService>();

        var checking = (
            await accountService.CreateAsync(
                $"Checking-{Guid.NewGuid():N}"[..24],
                AccountType.Asset,
                Eur,
                cancellationToken
            )
        ).Value!;
        var checkingBank = (
            await bankAccountService.CreateAsync(
                OwnBankAccount(
                    BankAccountType.Current,
                    iban: $"NL69INGB{NextDigits(10)}",
                    accountNumber: null,
                    checking.Id
                ),
                cancellationToken
            )
        ).Value!;

        var deposit = (
            await accountService.CreateAsync(
                $"Bouwdepot-{Guid.NewGuid():N}"[..24],
                AccountType.Asset,
                Eur,
                cancellationToken
            )
        ).Value!;
        var depositBank = (
            await bankAccountService.CreateAsync(
                OwnBankAccount(
                    BankAccountType.Savings,
                    iban: null,
                    accountNumber: NextDigits(10),
                    deposit.Id
                ),
                cancellationToken
            )
        ).Value!;

        var income = (
            await accountService.CreateAsync(
                $"Deposit-Interest-{Guid.NewGuid():N}"[..24],
                AccountType.Income,
                Eur,
                cancellationToken
            )
        ).Value!;
        var interest = (
            await accountService.CreateAsync(
                $"Loan-Interest-{Guid.NewGuid():N}"[..24],
                AccountType.Expense,
                Eur,
                cancellationToken
            )
        ).Value!;
        var equity = (
            await accountService.CreateAsync(
                $"Opening-{Guid.NewGuid():N}"[..24],
                AccountType.Equity,
                Eur,
                cancellationToken
            )
        ).Value!;

        var lender = (
            await counterpartyService.CreateAsync(
                $"Lender-{Guid.NewGuid():N}"[..24],
                cancellationToken
            )
        ).Value!;

        return new Fixture(
            scope,
            checking.Id,
            checkingBank.Id,
            deposit.Id,
            depositBank.Id,
            income.Id,
            interest.Id,
            equity.Id,
            lender.Id
        );
    }

    private static CreateBankAccountInput OwnBankAccount(
        BankAccountType type,
        string? iban,
        string? accountNumber,
        AccountId accountId
    ) =>
        new(
            Type: type,
            Iban: iban,
            AccountNumber: accountNumber,
            CardIdentifier: null,
            FundingBankAccountId: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: Eur,
            ImporterKey: null,
            AccountId: accountId,
            CounterpartyId: null
        );

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
        AccountId CheckingAccountId,
        BankAccountId CheckingBankAccountId,
        AccountId DepositAccountId,
        BankAccountId DepositBankAccountId,
        AccountId IncomeAccountId,
        AccountId InterestAccountId,
        AccountId EquityAccountId,
        CounterpartyId LenderId
    ) : IAsyncDisposable
    {
        private IServiceProvider Services => Scope.ServiceProvider;

        public ILoanService LoanService => Services.GetRequiredService<ILoanService>();
        public ILoanProjectionService ProjectionService =>
            Services.GetRequiredService<ILoanProjectionService>();
        public IBankTransactionCategorizationService CategorizationService =>
            Services.GetRequiredService<IBankTransactionCategorizationService>();
        public IBankTransactionAttachService AttachService =>
            Services.GetRequiredService<IBankTransactionAttachService>();

        public ValueTask DisposeAsync() => Scope.DisposeAsync();

        public async Task<LoanDetailOutput> CreateLoanWithDepositAsync(
            long openingBalance,
            CancellationToken cancellationToken
        )
        {
            if (openingBalance > 0)
                await PostAsync(
                    DepositAccountId,
                    openingBalance,
                    EquityAccountId,
                    -openingBalance,
                    DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-2),
                    counterpartyId: null,
                    cancellationToken
                );

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var result = await LoanService.CreateAsync(
                new CreateLoanInput(
                    $"Loan-{Guid.NewGuid():N}"[..20],
                    LenderId,
                    InterestAccountId,
                    Eur,
                    $"Loan-Parent-{Guid.NewGuid():N}"[..24],
                    $"P{Guid.NewGuid():N}"[..16],
                    [
                        new CreateLoanPartInput(
                            "Part 1",
                            LoanRepaymentType.InterestOnly,
                            today.AddYears(-1),
                            today.AddYears(29),
                            AdoptAccountId: null,
                            NewAccount: new NewLoanPartAccountInput(
                                $"Part-{Guid.NewGuid():N}"[..24],
                                $"L{Guid.NewGuid():N}"[..16],
                                PartOpeningBalance,
                                today.AddYears(-1)
                            ),
                            RatePeriods:
                            [
                                new CreateLoanRatePeriodInput(today.AddYears(-1), PartRate, null),
                            ]
                        ),
                    ],
                    ConstructionDepositAccountId: DepositAccountId,
                    ConstructionDepositInterestIncomeAccountId: IncomeAccountId,
                    ConstructionDepositAnnualRatePercent: DepositRate
                ),
                cancellationToken
            );
            if (result.IsFailure)
                throw new InvalidOperationException(result.Error!.ToString());
            return result.Value!;
        }

        public async Task PostInterestCreditAsync(
            long amount,
            DateOnly date,
            CancellationToken ct
        ) =>
            await PostAsync(
                DepositAccountId,
                amount,
                IncomeAccountId,
                -amount,
                date,
                counterpartyId: null,
                ct
            );

        public async Task DrainDepositAsync(long amount, CancellationToken ct) =>
            await PostAsync(
                DepositAccountId,
                -amount,
                EquityAccountId,
                amount,
                DateOnly.FromDateTime(DateTime.UtcNow),
                counterpartyId: null,
                ct
            );

        public async Task<JournalEntryDetailOutput> PostLoanPaymentAsync(
            LoanDetailOutput loan,
            CancellationToken cancellationToken
        )
        {
            var netDebit = MonthlyInterest - MonthlySettlement;
            var bt = await CreateCheckingDebitAsync(-netDebit, cancellationToken);
            var result = await CategorizationService.CategorizeAsync(
                bt.Id,
                new CategorizeBankTransactionInput(
                    CounterpartyId: LenderId,
                    NewCounterparty: null,
                    Date: bt.BookingDate,
                    Description: "Monthly payment",
                    Lines:
                    [
                        new CategorizeBankTransactionLineInput(
                            InterestAccountId,
                            MonthlyInterest,
                            "interest",
                            loan.Parts[0].Id
                        ),
                        new CategorizeBankTransactionLineInput(
                            DepositAccountId,
                            -MonthlySettlement,
                            "deposit settlement"
                        ),
                    ]
                ),
                cancellationToken
            );
            if (result.IsFailure)
                throw new InvalidOperationException(result.Error!.ToString());
            return result.Value!;
        }

        public async Task<JournalEntryId> PostNonLoanDepositEntryAsync(
            long depositLineAmount,
            CancellationToken cancellationToken
        )
        {
            var entry = await PostAsync(
                DepositAccountId,
                depositLineAmount,
                IncomeAccountId,
                -depositLineAmount,
                DateOnly.FromDateTime(DateTime.UtcNow),
                counterpartyId: LenderId,
                cancellationToken
            );
            return entry.Id;
        }

        public async Task<long> DepositBalanceAsync(CancellationToken cancellationToken)
        {
            await using var scope = Scope
                .ServiceProvider.GetRequiredService<IServiceScopeFactory>()
                .CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<BalanceDbContext>();
            return await dbContext
                    .JournalLines.Where(l => l.AccountId == DepositAccountId)
                    .SumAsync(l => (long?)l.Amount, cancellationToken)
                ?? 0L;
        }

        public Task<BankTransactionOutput> CreateCheckingDebitAsync(
            long amount,
            CancellationToken ct
        ) => CreateBtAsync(CheckingBankAccountId, amount, LenderIban, "Big Bank", ct);

        public Task<BankTransactionOutput> CreateDepositCreditBtAsync(
            long amount,
            CancellationToken ct
        ) =>
            CreateBtAsync(
                DepositBankAccountId,
                amount,
                counterpartyAccountNumber: null,
                "Rentevergoeding",
                ct
            );

        public Task<BankTransactionOutput> CreateDepositDebitBtAsync(
            long amount,
            CancellationToken ct
        ) =>
            CreateBtAsync(
                DepositBankAccountId,
                amount,
                counterpartyAccountNumber: null,
                "Verrekening",
                ct
            );

        private const string LenderIban = "NL77BIGB0001234567";

        private async Task<BankTransactionOutput> CreateBtAsync(
            BankAccountId bankAccountId,
            long amount,
            string? counterpartyAccountNumber,
            string counterpartyName,
            CancellationToken cancellationToken
        )
        {
            var svc = Services.GetRequiredService<IBankTransactionService>();
            var result = await svc.CreateAsync(
                new CreateBankTransactionInput(
                    BankAccountId: bankAccountId,
                    BookingDate: DateOnly.FromDateTime(DateTime.UtcNow),
                    Amount: amount,
                    CurrencyCode: Eur,
                    Description: "deposit-test",
                    CounterpartyName: counterpartyName,
                    CounterpartyAccountNumber: counterpartyAccountNumber
                ),
                cancellationToken
            );
            if (result.IsFailure)
                throw new InvalidOperationException(result.Error!.ToString());
            return result.Value!;
        }

        private async Task<JournalEntryDetailOutput> PostAsync(
            AccountId debitAccount,
            long debitAmount,
            AccountId creditAccount,
            long creditAmount,
            DateOnly date,
            CounterpartyId? counterpartyId,
            CancellationToken cancellationToken
        )
        {
            var svc = Services.GetRequiredService<IJournalEntryService>();
            var result = await svc.CreateAsync(
                new CreateJournalEntryInput(
                    Date: date,
                    Description: "seed",
                    CounterpartyId: counterpartyId,
                    Lines:
                    [
                        new CreateJournalLineInput(
                            debitAccount,
                            debitAmount,
                            null,
                            ReconciliationStatus.Uncleared
                        ),
                        new CreateJournalLineInput(
                            creditAccount,
                            creditAmount,
                            null,
                            ReconciliationStatus.Uncleared
                        ),
                    ]
                ),
                cancellationToken
            );
            if (result.IsFailure)
                throw new InvalidOperationException(result.Error!.ToString());
            return result.Value!;
        }
    }
}
