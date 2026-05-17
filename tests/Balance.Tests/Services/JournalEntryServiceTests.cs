using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Tests.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Balance.Tests.Services;

internal sealed class JournalEntryServiceTests : EndpointsTestsBase
{
    [Test]
    public async Task CreateAsync_round_trips_through_fresh_DbContext()
    {
        using var scope = Factory.Services.CreateScope();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var journalEntryService = scope.ServiceProvider.GetRequiredService<IJournalEntryService>();

        var groceries = await accountService.CreateAsync(
            $"Groceries-svc-{Guid.NewGuid():N}",
            AccountType.Expense,
            new CurrencyCode("EUR"),
            CancellationToken.None
        );
        var checking = await accountService.CreateAsync(
            $"Checking-svc-{Guid.NewGuid():N}",
            AccountType.Asset,
            new CurrencyCode("EUR"),
            CancellationToken.None
        );

        var created = await journalEntryService.CreateAsync(
            new CreateJournalEntryInput(
                Date: new DateOnly(2026, 5, 17),
                Description: "svc round-trip",
                BankTransactionId: null,
                CounterpartyId: null,
                Lines:
                [
                    new CreateJournalLineInput(groceries.Id, 4000, "AH"),
                    new CreateJournalLineInput(checking.Id, -4000, null),
                ]
            ),
            CancellationToken.None
        );

        using var verifyScope = Factory.Services.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<BalanceDbContext>();
        var reloaded = await dbContext
            .JournalEntries.AsNoTracking()
            .Include(e => e.Lines)
            .SingleAsync(e => e.Id == created.Id, CancellationToken.None);

        await Assert.That(reloaded.Date).IsEqualTo(new DateOnly(2026, 5, 17));
        await Assert.That(reloaded.Description).IsEqualTo("svc round-trip");
        await Assert.That(reloaded.Lines).Count().IsEqualTo(2);
        await Assert.That(reloaded.Lines.Sum(l => l.Amount)).IsEqualTo(0L);
        await Assert.That(reloaded.Lines.Single(l => l.AccountId == groceries.Id).Amount)
            .IsEqualTo(4000L);
        await Assert.That(reloaded.Lines.Single(l => l.AccountId == checking.Id).Amount)
            .IsEqualTo(-4000L);
        await Assert.That(reloaded.Lines.All(l =>
                l.ReconciliationStatus == ReconciliationStatus.Uncleared
            ))
            .IsTrue();
    }
}
