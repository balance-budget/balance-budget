using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.JournalEntries;

namespace Balance.Tests.Services;

internal sealed class JournalEntryProjectionTests
{
    private static readonly AccountId Checking = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111")
    );
    private static readonly AccountId Savings = new(
        Guid.Parse("22222222-2222-2222-2222-222222222222")
    );
    private static readonly AccountId Groceries = new(
        Guid.Parse("33333333-3333-3333-3333-333333333333")
    );
    private static readonly AccountId Household = new(
        Guid.Parse("44444444-4444-4444-4444-444444444444")
    );
    private static readonly AccountId Toiletries = new(
        Guid.Parse("55555555-5555-5555-5555-555555555555")
    );
    private static readonly AccountId Salary = new(
        Guid.Parse("66666666-6666-6666-6666-666666666666")
    );
    private static readonly AccountId CreditCard = new(
        Guid.Parse("77777777-7777-7777-7777-777777777777")
    );
    private static readonly AccountId Mortgage = new(
        Guid.Parse("88888888-8888-8888-8888-888888888888")
    );
    private static readonly AccountId OpeningBalance = new(
        Guid.Parse("99999999-9999-9999-9999-999999999999")
    );
    private static readonly AccountId TaxWithheld = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")
    );
    private static readonly AccountId Bonus = new(
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
    );

    [Test]
    public async Task Asset_to_Expense_is_loss_simplifiable()
    {
        // Groceries at €40 paid from Checking.
        var result = JournalEntryProjection.Compute([
            Line(Checking, "Checking", AccountType.Asset, -4000),
            Line(Groceries, "Groceries", AccountType.Expense, 4000),
        ]);

        await Assert.That(result.NetWorthChange).IsEqualTo(-4000L);
        await Assert.That(result.IsTransfer).IsFalse();
        await Assert.That(result.GrossMagnitude).IsEqualTo(4000L);
        await Assert.That(result.IsSimplifiable).IsTrue();
        await Assert.That(result.FromLegs.Select(l => l.AccountId)).IsEquivalentTo([Checking]);
        await Assert.That(result.ToLegs.Select(l => l.AccountId)).IsEquivalentTo([Groceries]);
    }

    [Test]
    public async Task Income_to_Asset_is_gain_simplifiable()
    {
        // €2500 salary into Checking.
        var result = JournalEntryProjection.Compute([
            Line(Checking, "Checking", AccountType.Asset, 250000),
            Line(Salary, "Salary", AccountType.Income, -250000),
        ]);

        await Assert.That(result.NetWorthChange).IsEqualTo(250000L);
        await Assert.That(result.IsTransfer).IsFalse();
        await Assert.That(result.GrossMagnitude).IsEqualTo(250000L);
        await Assert.That(result.IsSimplifiable).IsTrue();
        await Assert.That(result.FromLegs.Select(l => l.AccountId)).IsEquivalentTo([Salary]);
        await Assert.That(result.ToLegs.Select(l => l.AccountId)).IsEquivalentTo([Checking]);
    }

    [Test]
    public async Task Asset_to_Asset_transfer_is_zero_change()
    {
        // Checking -> Savings transfer.
        var result = JournalEntryProjection.Compute([
            Line(Checking, "Checking", AccountType.Asset, -100000),
            Line(Savings, "Savings", AccountType.Asset, 100000),
        ]);

        await Assert.That(result.NetWorthChange).IsEqualTo(0L);
        await Assert.That(result.IsTransfer).IsTrue();
        await Assert.That(result.GrossMagnitude).IsEqualTo(100000L);
        await Assert.That(result.IsSimplifiable).IsTrue();
        await Assert.That(result.FromLegs.Select(l => l.AccountId)).IsEquivalentTo([Checking]);
        await Assert.That(result.ToLegs.Select(l => l.AccountId)).IsEquivalentTo([Savings]);
    }

    [Test]
    public async Task Asset_to_Liability_credit_card_payment_is_zero_change()
    {
        // Pay €200 off the credit card from Checking.
        // Checking (Asset) credited -20000; CreditCard (Liability) debited +20000 (liability balance reduced).
        var result = JournalEntryProjection.Compute([
            Line(Checking, "Checking", AccountType.Asset, -20000),
            Line(CreditCard, "Credit Card", AccountType.Liability, 20000),
        ]);

        await Assert.That(result.NetWorthChange).IsEqualTo(0L);
        await Assert.That(result.IsTransfer).IsTrue();
        await Assert.That(result.GrossMagnitude).IsEqualTo(20000L);
        await Assert.That(result.IsSimplifiable).IsTrue();
    }

    [Test]
    public async Task Liability_to_Asset_loan_disbursement_is_zero_change()
    {
        // Bank deposits €5000 of loan into Checking.
        // Checking (Asset) +500000; Mortgage (Liability) -500000 (liability balance increased).
        var result = JournalEntryProjection.Compute([
            Line(Checking, "Checking", AccountType.Asset, 500000),
            Line(Mortgage, "Mortgage", AccountType.Liability, -500000),
        ]);

        await Assert.That(result.NetWorthChange).IsEqualTo(0L);
        await Assert.That(result.IsTransfer).IsTrue();
        await Assert.That(result.GrossMagnitude).IsEqualTo(500000L);
        await Assert.That(result.IsSimplifiable).IsTrue();
    }

    [Test]
    public async Task Equity_to_Asset_opening_balance_is_gain()
    {
        // €1000 opening balance: Checking +100000 / Opening Balance Equity -100000.
        var result = JournalEntryProjection.Compute([
            Line(Checking, "Checking", AccountType.Asset, 100000),
            Line(OpeningBalance, "Opening Balance", AccountType.Equity, -100000),
        ]);

        await Assert.That(result.NetWorthChange).IsEqualTo(100000L);
        await Assert.That(result.IsTransfer).IsFalse();
        await Assert.That(result.GrossMagnitude).IsEqualTo(100000L);
        await Assert.That(result.IsSimplifiable).IsTrue();
    }

    [Test]
    public async Task One_source_N_destinations_split_is_simplifiable_on_credit_side()
    {
        // €100 weekly shop split into Groceries €60, Household €25, Toiletries €15.
        var result = JournalEntryProjection.Compute([
            Line(Checking, "Checking", AccountType.Asset, -10000),
            Line(Groceries, "Groceries", AccountType.Expense, 6000),
            Line(Household, "Household", AccountType.Expense, 2500),
            Line(Toiletries, "Toiletries", AccountType.Expense, 1500),
        ]);

        await Assert.That(result.NetWorthChange).IsEqualTo(-10000L);
        await Assert.That(result.IsTransfer).IsFalse();
        await Assert.That(result.GrossMagnitude).IsEqualTo(10000L);
        await Assert.That(result.IsSimplifiable).IsTrue();
        await Assert.That(result.FromLegs.Select(l => l.AccountId)).IsEquivalentTo([Checking]);
        await Assert
            .That(result.ToLegs.Select(l => l.AccountId))
            .IsEquivalentTo([Groceries, Household, Toiletries]);
    }

    [Test]
    public async Task N_sources_one_destination_split_is_simplifiable_on_debit_side()
    {
        // Mortgage payment €1000 split: €600 from Checking + €400 from Savings.
        // Debit Mortgage +100000 (liability reduced).
        var result = JournalEntryProjection.Compute([
            Line(Mortgage, "Mortgage", AccountType.Liability, 100000),
            Line(Checking, "Checking", AccountType.Asset, -60000),
            Line(Savings, "Savings", AccountType.Asset, -40000),
        ]);

        await Assert.That(result.NetWorthChange).IsEqualTo(0L);
        await Assert.That(result.IsTransfer).IsTrue();
        await Assert.That(result.GrossMagnitude).IsEqualTo(100000L);
        await Assert.That(result.IsSimplifiable).IsTrue();
        await Assert
            .That(result.FromLegs.Select(l => l.AccountId))
            .IsEquivalentTo([Checking, Savings]);
        await Assert.That(result.ToLegs.Select(l => l.AccountId)).IsEquivalentTo([Mortgage]);
    }

    [Test]
    public async Task Multi_source_multi_destination_is_not_simplifiable()
    {
        // Paycheck: gross Salary -300000 + Bonus -50000 → Tax +100000 + Checking +250000.
        var result = JournalEntryProjection.Compute([
            Line(Salary, "Salary", AccountType.Income, -300000),
            Line(Bonus, "Bonus", AccountType.Income, -50000),
            Line(TaxWithheld, "Tax Withheld", AccountType.Expense, 100000),
            Line(Checking, "Checking", AccountType.Asset, 250000),
        ]);

        // Asset = +250000; Liability = 0 → NetWorthChange = +250000.
        await Assert.That(result.NetWorthChange).IsEqualTo(250000L);
        await Assert.That(result.IsTransfer).IsFalse();
        await Assert.That(result.GrossMagnitude).IsEqualTo(350000L);
        await Assert.That(result.IsSimplifiable).IsFalse();
        await Assert.That(result.FromLegs).IsEmpty();
        await Assert.That(result.ToLegs).IsEmpty();
    }

    [Test]
    public async Task Gross_magnitude_equals_sum_of_debits()
    {
        // Verify the invariant: GrossMagnitude == Σ positive amounts (the debits).
        var result = JournalEntryProjection.Compute([
            Line(Checking, "Checking", AccountType.Asset, -7500),
            Line(Groceries, "Groceries", AccountType.Expense, 3000),
            Line(Household, "Household", AccountType.Expense, 4500),
        ]);

        await Assert.That(result.GrossMagnitude).IsEqualTo(7500L);
    }

    [Test]
    public async Task Empty_lines_returns_defaults()
    {
        var result = JournalEntryProjection.Compute([]);

        await Assert.That(result.NetWorthChange).IsEqualTo(0L);
        await Assert.That(result.IsTransfer).IsTrue();
        await Assert.That(result.GrossMagnitude).IsEqualTo(0L);
        await Assert.That(result.IsSimplifiable).IsFalse();
        await Assert.That(result.FromLegs).IsEmpty();
        await Assert.That(result.ToLegs).IsEmpty();
    }

    private static JournalLineProjectionInput Line(
        AccountId id,
        string name,
        AccountType type,
        long amount
    ) => new(id, name, type, amount);
}
