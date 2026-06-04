using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

internal sealed class ReportsServiceTests : EndpointsTestsBase
{
    private static readonly DateOnly From = new(2026, 1, 1);
    private static readonly DateOnly To = new(2026, 1, 31);

    [Test]
    public async Task GetDistribution_rolls_up_subtrees_and_sorts_descending(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);

        var checking = await fx.AccountAsync("Checking", AccountType.Asset, cancellationToken);
        var food = await fx.PlaceholderAsync("Food", AccountType.Expense, cancellationToken);
        var groceries = await fx.ChildAsync("Groceries", food, cancellationToken);
        var dining = await fx.ChildAsync("Dining", food, cancellationToken);
        var rent = await fx.AccountAsync("Rent", AccountType.Expense, cancellationToken);

        await fx.EntryAsync(
            new(2026, 1, 5),
            cancellationToken,
            (groceries, 4000),
            (checking, -4000)
        );
        await fx.EntryAsync(new(2026, 1, 9), cancellationToken, (dining, 1000), (checking, -1000));
        await fx.EntryAsync(new(2026, 1, 20), cancellationToken, (rent, 12000), (checking, -12000));
        // Out of range — must be excluded.
        await fx.EntryAsync(
            new(2025, 12, 28),
            cancellationToken,
            (groceries, 9999),
            (checking, -9999)
        );

        var result = await fx.Reports.GetDistributionAsync(
            DistributionType.Expense,
            parentAccountId: null,
            From,
            To,
            fx.Currency,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var output = result.Value!;
        await Assert.That(output.Slices.Count).IsEqualTo(2);
        // Descending by amount: Rent (12000) before Food (5000).
        await Assert.That(output.Slices[0].Name).IsEqualTo("Rent");
        await Assert.That(output.Slices[0].Amount.Amount).IsEqualTo(12000L);
        await Assert.That(output.Slices[0].HasChildren).IsFalse();
        await Assert.That(output.Slices[1].Name).IsEqualTo("Food");
        await Assert.That(output.Slices[1].Amount.Amount).IsEqualTo(5000L);
        await Assert.That(output.Slices[1].HasChildren).IsTrue();
        await Assert.That(output.Total.Amount).IsEqualTo(17000L);
    }

    [Test]
    public async Task GetDistribution_drills_into_children_by_parent(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);

        var checking = await fx.AccountAsync("Checking", AccountType.Asset, cancellationToken);
        var food = await fx.PlaceholderAsync("Food", AccountType.Expense, cancellationToken);
        var groceries = await fx.ChildAsync("Groceries", food, cancellationToken);
        var dining = await fx.ChildAsync("Dining", food, cancellationToken);

        await fx.EntryAsync(
            new(2026, 1, 5),
            cancellationToken,
            (groceries, 4000),
            (checking, -4000)
        );
        await fx.EntryAsync(new(2026, 1, 9), cancellationToken, (dining, 1000), (checking, -1000));

        var result = await fx.Reports.GetDistributionAsync(
            DistributionType.Expense,
            parentAccountId: food,
            From,
            To,
            fx.Currency,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var output = result.Value!;
        await Assert.That(output.ParentAccountId).IsEqualTo((AccountId?)food);
        await Assert.That(output.Slices.Count).IsEqualTo(2);
        await Assert.That(output.Slices[0].Name).IsEqualTo("Groceries");
        await Assert.That(output.Slices[0].Amount.Amount).IsEqualTo(4000L);
        await Assert.That(output.Slices[1].Name).IsEqualTo("Dining");
        await Assert.That(output.Slices[1].Amount.Amount).IsEqualTo(1000L);
    }

    [Test]
    public async Task GetDistribution_nets_refunds_and_keeps_negative_sign(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);

        var checking = await fx.AccountAsync("Checking", AccountType.Asset, cancellationToken);
        var electronics = await fx.AccountAsync(
            "Electronics",
            AccountType.Expense,
            cancellationToken
        );

        // A refund credited to the expense exceeds the period's spend → net negative.
        await fx.EntryAsync(
            new(2026, 1, 4),
            cancellationToken,
            (electronics, 5000),
            (checking, -5000)
        );
        await fx.EntryAsync(
            new(2026, 1, 18),
            cancellationToken,
            (electronics, -6000),
            (checking, 6000)
        );

        var result = await fx.Reports.GetDistributionAsync(
            DistributionType.Expense,
            parentAccountId: null,
            From,
            To,
            fx.Currency,
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var slice = result.Value!.Slices.Single(s => s.Name == "Electronics");
        await Assert.That(slice.Amount.Amount).IsEqualTo(-1000L);
    }

    [Test]
    public async Task GetDistribution_unknown_parent_returns_not_found(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);

        var result = await fx.Reports.GetDistributionAsync(
            DistributionType.Expense,
            parentAccountId: AccountId.New(),
            From,
            To,
            fx.Currency,
            cancellationToken
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<NotFoundError>();
    }

    [Test]
    public async Task GetDistribution_with_from_after_to_returns_validation_error(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);

        var result = await fx.Reports.GetDistributionAsync(
            DistributionType.Income,
            parentAccountId: null,
            To,
            From,
            fx.Currency,
            cancellationToken
        );

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<ValidationError>();
    }

    [Test]
    public async Task GetMoneyFlow_balances_and_places_accounts_by_sign(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);

        var checking = await fx.AccountAsync("Checking", AccountType.Asset, cancellationToken);
        var brokerage = await fx.AccountAsync("Brokerage", AccountType.Asset, cancellationToken);
        var salary = await fx.AccountAsync("Salary", AccountType.Income, cancellationToken);
        var rent = await fx.AccountAsync("Rent", AccountType.Expense, cancellationToken);
        var groceries = await fx.AccountAsync("Groceries", AccountType.Expense, cancellationToken);

        // Salary in, rent + groceries out, surplus parked in a brokerage purchase (self-transfer).
        await fx.EntryAsync(new(2026, 1, 1), cancellationToken, (checking, 3000), (salary, -3000));
        await fx.EntryAsync(new(2026, 1, 6), cancellationToken, (rent, 1000), (checking, -1000));
        await fx.EntryAsync(new(2026, 1, 9), cancellationToken, (groceries, 500), (checking, -500));
        await fx.EntryAsync(
            new(2026, 1, 12),
            cancellationToken,
            (brokerage, 1000),
            (checking, -1000)
        );

        var result = await fx.Reports.GetMoneyFlowAsync(
            From,
            To,
            fx.Currency,
            new HashSet<AccountId>(),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var flow = result.Value!;

        // Sources (into hub) and exits (out of hub) balance by the double-entry identity.
        var inbound = flow.Links.Where(l => l.Target == "hub").Sum(l => l.Value.Amount);
        var outbound = flow.Links.Where(l => l.Source == "hub").Sum(l => l.Value.Amount);
        await Assert.That(inbound).IsEqualTo(3000L);
        await Assert.That(outbound).IsEqualTo(3000L);

        // Salary is a source: Salary → hub, value 3000.
        var salaryLink = flow.Links.Single(l => l.Source == salary.Value.ToString());
        await Assert.That(salaryLink.Target).IsEqualTo("hub");
        await Assert.That(salaryLink.Value.Amount).IsEqualTo(3000L);

        // Brokerage grew → exit node: hub → Brokerage, value 1000.
        var brokerageLink = flow.Links.Single(l => l.Target == brokerage.Value.ToString());
        await Assert.That(brokerageLink.Source).IsEqualTo("hub");
        await Assert.That(brokerageLink.Value.Amount).IsEqualTo(1000L);

        // Cash buffer that stayed in checking shows as an exit too (3000 − 1000 − 500 − 1000).
        var checkingLink = flow.Links.Single(l => l.Target == checking.Value.ToString());
        await Assert.That(checkingLink.Value.Amount).IsEqualTo(500L);
    }

    [Test]
    public async Task GetMoneyFlow_nests_expanded_expense_subtree_as_intermediate_nodes(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);

        var checking = await fx.AccountAsync("Checking", AccountType.Asset, cancellationToken);
        var salary = await fx.AccountAsync("Salary", AccountType.Income, cancellationToken);
        var food = await fx.PlaceholderAsync("Food", AccountType.Expense, cancellationToken);
        var groceries = await fx.ChildAsync("Groceries", food, cancellationToken);
        var dining = await fx.ChildAsync("Dining", food, cancellationToken);

        await fx.EntryAsync(new(2026, 1, 1), cancellationToken, (checking, 6000), (salary, -6000));
        await fx.EntryAsync(
            new(2026, 1, 6),
            cancellationToken,
            (groceries, 4000),
            (checking, -4000)
        );
        await fx.EntryAsync(new(2026, 1, 9), cancellationToken, (dining, 1000), (checking, -1000));

        // Expanding Food draws its children as intermediate nodes; without it Food would collapse.
        var result = await fx.Reports.GetMoneyFlowAsync(
            From,
            To,
            fx.Currency,
            new HashSet<AccountId> { food },
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var flow = result.Value!;

        // The placeholder is an intermediate node: hub → Food (5000) → {Groceries 4000, Dining 1000}.
        var hubToFood = flow.Links.Single(l =>
            l.Source == "hub" && l.Target == food.Value.ToString()
        );
        await Assert.That(hubToFood.Value.Amount).IsEqualTo(5000L);
        var foodToGroceries = flow.Links.Single(l =>
            l.Source == food.Value.ToString() && l.Target == groceries.Value.ToString()
        );
        await Assert.That(foodToGroceries.Value.Amount).IsEqualTo(4000L);
        var foodToDining = flow.Links.Single(l =>
            l.Source == food.Value.ToString() && l.Target == dining.Value.ToString()
        );
        await Assert.That(foodToDining.Value.Amount).IsEqualTo(1000L);

        // Child nodes carry their parent id so the client can prune them when Food collapses.
        var groceriesNode = flow.Nodes.Single(n => n.Id == groceries.Value.ToString());
        await Assert.That(groceriesNode.ParentId).IsEqualTo(food.Value.ToString());

        // And it still balances.
        var inbound = flow.Links.Where(l => l.Target == "hub").Sum(l => l.Value.Amount);
        var outbound = flow.Links.Where(l => l.Source == "hub").Sum(l => l.Value.Amount);
        await Assert.That(inbound).IsEqualTo(outbound);
    }

    [Test]
    public async Task GetMoneyFlow_collapses_unexpanded_subtree_into_the_root(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);

        var checking = await fx.AccountAsync("Checking", AccountType.Asset, cancellationToken);
        var salary = await fx.AccountAsync("Salary", AccountType.Income, cancellationToken);
        var food = await fx.PlaceholderAsync("Food", AccountType.Expense, cancellationToken);
        var groceries = await fx.ChildAsync("Groceries", food, cancellationToken);
        var dining = await fx.ChildAsync("Dining", food, cancellationToken);

        await fx.EntryAsync(new(2026, 1, 1), cancellationToken, (checking, 6000), (salary, -6000));
        await fx.EntryAsync(
            new(2026, 1, 6),
            cancellationToken,
            (groceries, 4000),
            (checking, -4000)
        );
        await fx.EntryAsync(new(2026, 1, 9), cancellationToken, (dining, 1000), (checking, -1000));

        // Empty expanded set: Food is not opted in, so it stays collapsed.
        var result = await fx.Reports.GetMoneyFlowAsync(
            From,
            To,
            fx.Currency,
            new HashSet<AccountId>(),
            cancellationToken
        );

        await Assert.That(result.IsSuccess).IsTrue();
        var flow = result.Value!;

        // Food collapses to a single hub → Food (5000) link; its children are gone, but the node
        // advertises that expanding it would reveal them.
        var hubToFood = flow.Links.Single(l =>
            l.Source == "hub" && l.Target == food.Value.ToString()
        );
        await Assert.That(hubToFood.Value.Amount).IsEqualTo(5000L);
        await Assert.That(flow.Links.Any(l => l.Source == food.Value.ToString())).IsFalse();
        await Assert.That(flow.Nodes.Any(n => n.Id == groceries.Value.ToString())).IsFalse();
        await Assert.That(flow.Nodes.Any(n => n.Id == dining.Value.ToString())).IsFalse();
        await Assert
            .That(flow.Nodes.Single(n => n.Id == food.Value.ToString()).HasChildren)
            .IsTrue();

        // Collapsing only folds nodes together; the double-entry balance is untouched.
        var inbound = flow.Links.Where(l => l.Target == "hub").Sum(l => l.Value.Amount);
        var outbound = flow.Links.Where(l => l.Source == "hub").Sum(l => l.Value.Amount);
        await Assert.That(inbound).IsEqualTo(6000L);
        await Assert.That(outbound).IsEqualTo(6000L);
    }

    private async Task<Fixture> CreateFixtureAsync(CancellationToken cancellationToken)
    {
        var scope = Factory.Services.CreateAsyncScope();
        var currencies = scope.ServiceProvider.GetRequiredService<ICurrencyService>();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var entries = scope.ServiceProvider.GetRequiredService<IJournalEntryService>();
        var reports = scope.ServiceProvider.GetRequiredService<IReportsService>();

        // Unique currency keeps each test isolated from seeded accounts/currencies.
        var currency = new CurrencyCode("TST");
        var created = await currencies.CreateAsync(
            new CreateCurrencyInput(currency, "Test", 2, "T"),
            cancellationToken
        );
        await Assert.That(created.IsSuccess).IsTrue();

        return new Fixture(scope, accounts, entries, reports, currency);
    }

    private sealed record Fixture(
        AsyncServiceScope Scope,
        IAccountService Accounts,
        IJournalEntryService Entries,
        IReportsService Reports,
        CurrencyCode Currency
    ) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Scope.DisposeAsync();

        public async Task<AccountId> AccountAsync(
            string name,
            AccountType type,
            CancellationToken cancellationToken
        )
        {
            var result = await Accounts.CreateAsync(
                new CreateAccountInput
                {
                    Name = name,
                    Code = $"T{Guid.NewGuid():N}"[..16],
                    AccountType = type,
                    CurrencyCode = Currency,
                },
                cancellationToken
            );
            await Assert.That(result.IsSuccess).IsTrue();
            return result.Value!.Id;
        }

        public async Task<AccountId> PlaceholderAsync(
            string name,
            AccountType type,
            CancellationToken cancellationToken
        )
        {
            var result = await Accounts.CreateAsync(
                new CreateAccountInput
                {
                    Name = name,
                    Code = $"T{Guid.NewGuid():N}"[..16],
                    AccountType = type,
                    CurrencyCode = Currency,
                    IsPostable = false,
                },
                cancellationToken
            );
            await Assert.That(result.IsSuccess).IsTrue();
            return result.Value!.Id;
        }

        public async Task<AccountId> ChildAsync(
            string name,
            AccountId parent,
            CancellationToken cancellationToken
        )
        {
            var parentAccount = (await Accounts.GetAsync(parent, cancellationToken)).Value!;
            var result = await Accounts.CreateAsync(
                new CreateAccountInput
                {
                    Name = name,
                    Code = $"T{Guid.NewGuid():N}"[..16],
                    AccountType = parentAccount.AccountType,
                    CurrencyCode = Currency,
                    ParentAccountId = parent,
                },
                cancellationToken
            );
            await Assert.That(result.IsSuccess).IsTrue();
            return result.Value!.Id;
        }

        public async Task EntryAsync(
            DateOnly date,
            CancellationToken cancellationToken,
            params (AccountId Account, long Amount)[] lines
        )
        {
            var result = await Entries.CreateAsync(
                new CreateJournalEntryInput(
                    date,
                    "report-test",
                    CounterpartyId: null,
                    Lines:
                    [
                        .. lines.Select(l => new CreateJournalLineInput(l.Account, l.Amount, null)),
                    ]
                ),
                cancellationToken
            );
            await Assert.That(result.IsSuccess).IsTrue();
        }
    }
}
