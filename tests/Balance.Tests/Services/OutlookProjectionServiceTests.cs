using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

/// <summary>
/// Integration coverage for the ledger-reading half of Outlook (ADR-0027): Typical spend from
/// trailing actuals, query-time Occurrence matching exclusion, forward template projection, and
/// candidate detection. Seeded relative to the real clock the host's TimeProvider reports, anchored
/// to month boundaries so the trailing window is deterministic.
/// </summary>
internal sealed class OutlookProjectionServiceTests : EndpointsTestsBase
{
    private static DateOnly AnchorMonth()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new DateOnly(today.Year, today.Month, 1);
    }

    /// <summary>A mid-month day in the month <paramref name="monthsBack"/> before the current one.</summary>
    private static DateOnly PriorMonthDay(int monthsBack, int day)
    {
        var month = AnchorMonth().AddMonths(-monthsBack);
        return new DateOnly(month.Year, month.Month, day);
    }

    [Test]
    public async Task TypicalSpend_uses_trailing_non_recurring_and_excludes_transfers(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);
        var checking = await fx.AccountAsync("Checking", AccountType.Asset, cancellationToken);
        var savings = await fx.AccountAsync("Savings", AccountType.Asset, cancellationToken);
        var groceries = await fx.AccountAsync("Groceries", AccountType.Expense, cancellationToken);

        // Each of the three trailing whole months: €3.00 groceries (a real P&L expense) plus a
        // €10.00 checking→savings transfer (no P&L leg) that must NOT count as Typical spend.
        for (var back = 1; back <= 3; back++)
        {
            await fx.EntryAsync(
                PriorMonthDay(back, 15),
                counterparty: null,
                cancellationToken,
                (groceries, 300),
                (checking, -300)
            );
            await fx.EntryAsync(
                PriorMonthDay(back, 10),
                counterparty: null,
                cancellationToken,
                (savings, 1000),
                (checking, -1000)
            );
        }

        var result = await fx.Outlook.GetProjectionAsync(fx.Currency, 3, null, cancellationToken);
        await Assert.That(result.IsSuccess).IsTrue();

        var checkingProjection = result.Value!.Accounts.Single(a => a.AccountId == checking);
        await Assert.That(checkingProjection.Baseline.Count).IsEqualTo(3);
        // Median across the three months is the groceries spend alone — the transfer is excluded,
        // or every month would read −1300 and the median with it.
        foreach (var month in checkingProjection.Baseline)
            await Assert.That(month.TypicalSpendMid).IsEqualTo(-300L);

        // The curve picks up from today's balance: first month-end = current + typical spend.
        await Assert
            .That(checkingProjection.Baseline[0].EndBalanceMid)
            .IsEqualTo(checkingProjection.CurrentBalance - 300L);
    }

    [Test]
    public async Task Template_projects_forward_and_is_excluded_from_typical_spend(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);
        var checking = await fx.AccountAsync("Checking", AccountType.Asset, cancellationToken);
        var utilities = await fx.AccountAsync("Utilities", AccountType.Expense, cancellationToken);
        var groceries = await fx.AccountAsync("Groceries", AccountType.Expense, cancellationToken);
        var vattenfall = await fx.CounterpartyAsync("Vattenfall", cancellationToken);

        // A confirmed monthly template: €61.00 out of checking to Vattenfall.
        var created = await fx.Templates.CreateAsync(
            new CreateJournalEntryTemplateInput(
                "Energy",
                checking,
                utilities,
                vattenfall,
                Cadence.Monthly,
                PriorMonthDay(3, 1),
                EndDate: null,
                ExpectedAmount: -6100,
                MandateId: null,
                SepaCreditorId: null
            ),
            cancellationToken
        );
        await Assert.That(created.IsSuccess).IsTrue();

        for (var back = 1; back <= 3; back++)
        {
            // Realized Vattenfall charges — should be matched to the template and excluded.
            await fx.EntryAsync(
                PriorMonthDay(back, 2),
                vattenfall,
                cancellationToken,
                (utilities, 6100),
                (checking, -6100)
            );
            // Unrelated everyday spend — should drive Typical spend.
            await fx.EntryAsync(
                PriorMonthDay(back, 18),
                counterparty: null,
                cancellationToken,
                (groceries, 300),
                (checking, -300)
            );
        }

        var result = await fx.Outlook.GetProjectionAsync(fx.Currency, 3, null, cancellationToken);
        await Assert.That(result.IsSuccess).IsTrue();

        var checkingProjection = result.Value!.Accounts.Single(a => a.AccountId == checking);
        foreach (var month in checkingProjection.Baseline)
        {
            // The template applies forward every month…
            await Assert.That(month.ExpectedNet).IsEqualTo(-6100L);
            // …and its realized occurrences are kept out of Typical spend (groceries only).
            await Assert.That(month.TypicalSpendMid).IsEqualTo(-300L);
        }
    }

    [Test]
    public async Task DetectCandidates_finds_monthly_recurring_from_history(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);
        var checking = await fx.AccountAsync("Checking", AccountType.Asset, cancellationToken);
        var subscriptions = await fx.AccountAsync(
            "Subscriptions",
            AccountType.Expense,
            cancellationToken
        );
        var spotify = await fx.CounterpartyAsync("Spotify", cancellationToken);

        for (var back = 1; back <= 3; back++)
        {
            await fx.EntryAsync(
                PriorMonthDay(back, 5),
                spotify,
                cancellationToken,
                (subscriptions, 1200),
                (checking, -1200)
            );
        }

        var candidates = await fx.Templates.DetectCandidatesAsync(cancellationToken);

        var spotifyCandidate = candidates.Single(c => c.CounterpartyId == spotify);
        await Assert.That(spotifyCandidate.AccountId).IsEqualTo(checking);
        await Assert.That(spotifyCandidate.Cadence).IsEqualTo(Cadence.Monthly);
        await Assert.That(spotifyCandidate.OccurrenceCount).IsEqualTo(3);
        await Assert.That(spotifyCandidate.ExpectedAmount).IsEqualTo(-1200L);
        await Assert.That(spotifyCandidate.MonthlyEquivalent).IsEqualTo(-1200L);
        await Assert.That(spotifyCandidate.SuggestedName).IsEqualTo("Spotify");
    }

    [Test]
    public async Task DetectCandidates_skips_patterns_already_covered_by_a_template(
        CancellationToken cancellationToken
    )
    {
        await using var fx = await CreateFixtureAsync(cancellationToken);
        var checking = await fx.AccountAsync("Checking", AccountType.Asset, cancellationToken);
        var subscriptions = await fx.AccountAsync(
            "Subscriptions",
            AccountType.Expense,
            cancellationToken
        );
        var spotify = await fx.CounterpartyAsync("Spotify", cancellationToken);

        var created = await fx.Templates.CreateAsync(
            new CreateJournalEntryTemplateInput(
                "Spotify",
                checking,
                subscriptions,
                spotify,
                Cadence.Monthly,
                PriorMonthDay(3, 5),
                EndDate: null,
                ExpectedAmount: -1200,
                MandateId: null,
                SepaCreditorId: null
            ),
            cancellationToken
        );
        await Assert.That(created.IsSuccess).IsTrue();

        for (var back = 1; back <= 3; back++)
        {
            await fx.EntryAsync(
                PriorMonthDay(back, 5),
                spotify,
                cancellationToken,
                (subscriptions, 1200),
                (checking, -1200)
            );
        }

        var candidates = await fx.Templates.DetectCandidatesAsync(cancellationToken);
        await Assert.That(candidates.Any(c => c.CounterpartyId == spotify)).IsFalse();
    }

    private async Task<Fixture> CreateFixtureAsync(CancellationToken cancellationToken)
    {
        var scope = Factory.Services.CreateAsyncScope();
        var currencies = scope.ServiceProvider.GetRequiredService<ICurrencyService>();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var counterparties = scope.ServiceProvider.GetRequiredService<ICounterpartyService>();
        var entries = scope.ServiceProvider.GetRequiredService<IJournalEntryService>();
        var templates = scope.ServiceProvider.GetRequiredService<IJournalEntryTemplateService>();
        var outlook = scope.ServiceProvider.GetRequiredService<IOutlookService>();

        var currency = new CurrencyCode("TST");
        var created = await currencies.CreateAsync(
            new CreateCurrencyInput(currency, "Test", 2, "T"),
            cancellationToken
        );
        await Assert.That(created.IsSuccess).IsTrue();

        return new Fixture(scope, accounts, counterparties, entries, templates, outlook, currency);
    }

    private sealed record Fixture(
        AsyncServiceScope Scope,
        IAccountService Accounts,
        ICounterpartyService Counterparties,
        IJournalEntryService Entries,
        IJournalEntryTemplateService Templates,
        IOutlookService Outlook,
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

        public async Task<CounterpartyId> CounterpartyAsync(
            string name,
            CancellationToken cancellationToken
        )
        {
            var result = await Counterparties.CreateAsync(name, cancellationToken);
            await Assert.That(result.IsSuccess).IsTrue();
            return result.Value!.Id;
        }

        public async Task EntryAsync(
            DateOnly date,
            CounterpartyId? counterparty,
            CancellationToken cancellationToken,
            params (AccountId Account, long Amount)[] lines
        )
        {
            var result = await Entries.CreateAsync(
                new CreateJournalEntryInput(
                    date,
                    "outlook-test",
                    counterparty,
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
