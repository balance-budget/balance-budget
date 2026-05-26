using System.Net;
using System.Net.Http.Json;
using Balance.Tests.Api.Helpers;

namespace Balance.Tests.Api;

internal sealed class BankTransactionEndpointsTests : EndpointsTestsBase
{
    [Test]
    public async Task ListBankTransactions_returns_ok()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/bank-transactions", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<BankTransactionDto>>();
        await Assert.That(list).IsNotNull();
    }

    [Test]
    public async Task CreateBankTransaction_for_owned_bank_account_round_trips()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-BTX-1");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL12RABO0BTX00001",
            accountId: account.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: -12345L,
            CurrencyCode: "EUR",
            Description: "Round-trip groceries",
            CounterpartyName: "Albert Heijn",
            CounterpartyAccountNumber: "NL44RABO0123456789"
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            request
        );

        await Assert.That(createResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<BankTransactionDto>();
        await Assert.That(created).IsNotNull();
        await Assert.That(created!.BankAccountId).IsEqualTo(bankAccount.Id);
        await Assert.That(created.Money.Amount).IsEqualTo(-12345L);
        await Assert.That(created.Money.CurrencyCode).IsEqualTo("EUR");
        await Assert.That(created.BookingDate).IsEqualTo(new DateOnly(2026, 5, 17));
        await Assert.That(created.Description).IsEqualTo("Round-trip groceries");
        await Assert.That(created.CounterpartyName).IsEqualTo("Albert Heijn");
        await Assert.That(created.CounterpartyAccountNumber).IsEqualTo("NL44RABO0123456789");

        using var getResponse = await client.GetAsync(
            new Uri($"/api/bank-transactions/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<BankTransactionDto>();
        await Assert.That(fetched!.Money.Amount).IsEqualTo(-12345L);
        await Assert.That(fetched.Money.CurrencyCode).IsEqualTo("EUR");
        await Assert.That(fetched.Description).IsEqualTo("Round-trip groceries");
        await Assert.That(fetched.CounterpartyName).IsEqualTo("Albert Heijn");
        await Assert.That(fetched.CounterpartyAccountNumber).IsEqualTo("NL44RABO0123456789");
    }

    [Test]
    public async Task CreateBankTransaction_on_counterparty_bank_account_returns_422()
    {
        using var client = Factory.CreateClient();
        var counterparty = await CreateCounterpartyAsync(client, "Counterparty-BTX-1");
        var bankAccount = await CreateBankAccountForCounterpartyAsync(
            client,
            iban: "NL31RABO0BTX00002",
            counterpartyId: counterparty.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 1000L,
            CurrencyCode: "EUR",
            Description: "Counterparty BTX"
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task CreateBankTransaction_unknown_bank_account_returns_404()
    {
        using var client = Factory.CreateClient();

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: Guid.NewGuid(),
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 250L,
            CurrencyCode: "EUR",
            Description: "Unknown BankAccount"
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateBankTransaction_unknown_currency_returns_404()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-BTX-Currency");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL01RABO0BTX00003",
            accountId: account.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 250L,
            CurrencyCode: "XYZ",
            Description: "Unknown currency"
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateBankTransaction_zero_amount_returns_400()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-BTX-Zero");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL02RABO0BTX00004",
            accountId: account.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 0L,
            CurrencyCode: "EUR",
            Description: "Zero amount"
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateBankTransaction_empty_description_returns_400()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-BTX-EmptyDesc");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL04RABO0BTX00006",
            accountId: account.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 100L,
            CurrencyCode: "EUR",
            Description: ""
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            request
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateBankTransaction_duplicate_payload_returns_409()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-BTX-Duplicate");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL05RABO0BTX00007",
            accountId: account.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 500L,
            CurrencyCode: "EUR",
            Description: "Same payload twice"
        );
        using var first = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            request
        );
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.Created);

        using var second = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            request
        );
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task GetBankTransaction_returns_404_when_unknown()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri($"/api/bank-transactions/{Guid.NewGuid()}", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteBankTransaction_removes_the_row()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-BTX-Delete");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL03RABO0BTX00005",
            accountId: account.Id
        );

        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 1L,
            CurrencyCode: "EUR",
            Description: "Delete me"
        );
        using var createResponse = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            request
        );
        var created = await createResponse.Content.ReadFromJsonAsync<BankTransactionDto>();

        using var deleteResponse = await client.DeleteAsync(
            new Uri($"/api/bank-transactions/{created!.Id}", UriKind.Relative)
        );
        await Assert.That(deleteResponse.StatusCode).IsEqualTo(HttpStatusCode.NoContent);

        using var getResponse = await client.GetAsync(
            new Uri($"/api/bank-transactions/{created.Id}", UriKind.Relative)
        );
        await Assert.That(getResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DismissBankTransaction_marks_row_dismissed_and_round_trips()
    {
        using var client = Factory.CreateClient();
        var btx = await CreateOwnedBankTransactionAsync(client, "NL10RABO0BTX01010", -1L);

        using var dismissResponse = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("Test transaction — ignore.")
        );

        await Assert.That(dismissResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dismissed = await dismissResponse.Content.ReadFromJsonAsync<BankTransactionDto>();
        await Assert.That(dismissed!.DismissedAt).IsNotNull();
        await Assert.That(dismissed.DismissedReason).IsEqualTo("Test transaction — ignore.");

        using var getResponse = await client.GetAsync(
            new Uri($"/api/bank-transactions/{btx.Id}", UriKind.Relative)
        );
        var fetched = await getResponse.Content.ReadFromJsonAsync<BankTransactionDto>();
        await Assert.That(fetched!.DismissedAt).IsNotNull();
        await Assert.That(fetched.DismissedReason).IsEqualTo("Test transaction — ignore.");
    }

    [Test]
    public async Task DismissBankTransaction_trims_reason()
    {
        using var client = Factory.CreateClient();
        var btx = await CreateOwnedBankTransactionAsync(client, "NL11RABO0BTX01011", -2L);

        using var dismissResponse = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("   Settled out of band.   ")
        );

        await Assert.That(dismissResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dismissed = await dismissResponse.Content.ReadFromJsonAsync<BankTransactionDto>();
        await Assert.That(dismissed!.DismissedReason).IsEqualTo("Settled out of band.");
    }

    [Test]
    public async Task DismissBankTransaction_empty_reason_returns_400()
    {
        using var client = Factory.CreateClient();
        var btx = await CreateOwnedBankTransactionAsync(client, "NL12RABO0BTX01012", -3L);

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("")
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task DismissBankTransaction_whitespace_reason_returns_400()
    {
        using var client = Factory.CreateClient();
        var btx = await CreateOwnedBankTransactionAsync(client, "NL13RABO0BTX01013", -4L);

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("   ")
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task DismissBankTransaction_already_dismissed_returns_409()
    {
        using var client = Factory.CreateClient();
        var btx = await CreateOwnedBankTransactionAsync(client, "NL14RABO0BTX01014", -5L);

        using var first = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("Ignore me.")
        );
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var second = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("Already done.")
        );
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task DismissBankTransaction_with_referencing_journal_entry_returns_409()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-BTX-Cat1");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL17RABO0BTX01017",
            accountId: account.Id
        );
        var btxRequest = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: new DateOnly(2026, 5, 17),
            Amount: 1234L,
            CurrencyCode: "EUR",
            Description: "Already categorised"
        );
        using var btxResponse = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            btxRequest
        );
        var btx = await btxResponse.Content.ReadFromJsonAsync<BankTransactionDto>();
        var counter = await CreateAccountAsync(client, "Income-BTX-Cat1");

        var jeRequest = new
        {
            Date = new DateOnly(2026, 5, 17),
            Description = "Linked entry",
            BankTransactionId = btx!.Id,
            CounterpartyId = (Guid?)null,
            Lines = new[]
            {
                new
                {
                    AccountId = account.Id,
                    Amount = 1234L,
                    Description = (string?)null,
                },
                new
                {
                    AccountId = counter.Id,
                    Amount = -1234L,
                    Description = (string?)null,
                },
            },
        };
        using var jeResponse = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            jeRequest
        );
        await Assert.That(jeResponse.StatusCode).IsEqualTo(HttpStatusCode.Created);

        using var dismiss = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("Try to dismiss after categorising.")
        );
        await Assert.That(dismiss.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task DismissBankTransaction_unknown_id_returns_404()
    {
        using var client = Factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{Guid.NewGuid()}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("Doesn't matter.")
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UndismissBankTransaction_clears_dismissal()
    {
        using var client = Factory.CreateClient();
        var btx = await CreateOwnedBankTransactionAsync(client, "NL15RABO0BTX01015", -6L);

        using var dismiss = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("Wrong call.")
        );
        await Assert.That(dismiss.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var undismiss = await client.PostAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/undismiss", UriKind.Relative),
            content: null
        );
        await Assert.That(undismiss.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var restored = await undismiss.Content.ReadFromJsonAsync<BankTransactionDto>();
        await Assert.That(restored!.DismissedAt).IsNull();
        await Assert.That(restored.DismissedReason).IsNull();
    }

    [Test]
    public async Task UndismissBankTransaction_when_not_dismissed_returns_422()
    {
        using var client = Factory.CreateClient();
        var btx = await CreateOwnedBankTransactionAsync(client, "NL16RABO0BTX01016", -7L);

        using var response = await client.PostAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/undismiss", UriKind.Relative),
            content: null
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.UnprocessableEntity);
    }

    [Test]
    public async Task UndismissBankTransaction_unknown_id_returns_404()
    {
        using var client = Factory.CreateClient();

        using var response = await client.PostAsync(
            new Uri($"/api/bank-transactions/{Guid.NewGuid()}/undismiss", UriKind.Relative),
            content: null
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ListBankTransactions_default_filter_excludes_dismissed_rows()
    {
        using var client = Factory.CreateClient();
        var inbox = await CreateOwnedBankTransactionAsync(client, "NL20RABO0BTX02001", -101L);
        var dismissed = await CreateOwnedBankTransactionAsync(client, "NL20RABO0BTX02002", -202L);

        using var dismiss = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{dismissed.Id}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("Default-filter exclusion test")
        );
        await Assert.That(dismiss.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var rows = await ListBankTransactionsAsync(client);
        await Assert.That(rows.Any(b => b.Id == inbox.Id)).IsTrue();
        await Assert.That(rows.Any(b => b.Id == dismissed.Id)).IsFalse();
    }

    [Test]
    public async Task ListBankTransactions_default_filter_excludes_matched_rows()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-LIST-Default-Matched");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL21RABO0BTX02003",
            accountId: account.Id
        );
        var btx = await CreateBankTransactionAsync(
            client,
            bankAccount.Id,
            new DateOnly(2026, 5, 17),
            500L,
            "Default-Matched inbox exclusion"
        );

        var counter = await CreateAccountAsync(client, "Income-LIST-Default-Matched");
        await CreateJournalEntryForBankTransactionAsync(
            client,
            btx.Id,
            account.Id,
            counter.Id,
            500L
        );

        var rows = await ListBankTransactionsAsync(client);
        await Assert.That(rows.Any(b => b.Id == btx.Id)).IsFalse();
    }

    [Test]
    public async Task ListBankTransactions_inbox_filter_sorts_oldest_first()
    {
        using var client = Factory.CreateClient();
        var older = await CreateOwnedBankTransactionAsync(
            client,
            "NL22RABO0BTX02010",
            -10L,
            new DateOnly(2024, 1, 15)
        );
        var newer = await CreateOwnedBankTransactionAsync(
            client,
            "NL22RABO0BTX02011",
            -11L,
            new DateOnly(2026, 7, 22)
        );

        var rows = await ListBankTransactionsAsync(client, "Inbox");
        var olderIdx = rows.ToList().FindIndex(b => b.Id == older.Id);
        var newerIdx = rows.ToList().FindIndex(b => b.Id == newer.Id);
        await Assert.That(olderIdx).IsGreaterThanOrEqualTo(0);
        await Assert.That(newerIdx).IsGreaterThanOrEqualTo(0);
        await Assert.That(olderIdx).IsLessThan(newerIdx);
    }

    [Test]
    public async Task ListBankTransactions_matched_filter_returns_only_rows_with_journal_entry()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-LIST-Matched");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL23RABO0BTX02020",
            accountId: account.Id
        );
        var matched = await CreateBankTransactionAsync(
            client,
            bankAccount.Id,
            new DateOnly(2026, 5, 17),
            777L,
            "Matched-filter target"
        );
        var counter = await CreateAccountAsync(client, "Income-LIST-Matched");
        var je = await CreateJournalEntryForBankTransactionAsync(
            client,
            matched.Id,
            account.Id,
            counter.Id,
            777L
        );

        var inboxRow = await CreateOwnedBankTransactionAsync(client, "NL23RABO0BTX02021", -55L);

        var rows = await ListBankTransactionsAsync(client, "Matched");
        var matchedRow = rows.SingleOrDefault(b => b.Id == matched.Id);
        await Assert.That(matchedRow).IsNotNull();
        await Assert.That(matchedRow!.JournalEntryId).IsEqualTo(je);
        await Assert.That(rows.Any(b => b.Id == inboxRow.Id)).IsFalse();
    }

    [Test]
    public async Task ListBankTransactions_dismissed_filter_returns_only_dismissed_rows()
    {
        using var client = Factory.CreateClient();
        var dismissed = await CreateOwnedBankTransactionAsync(client, "NL24RABO0BTX02030", -33L);
        using var dismiss = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{dismissed.Id}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("Dismissed-filter test")
        );
        await Assert.That(dismiss.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var inboxRow = await CreateOwnedBankTransactionAsync(client, "NL24RABO0BTX02031", -34L);

        var rows = await ListBankTransactionsAsync(client, "Dismissed");
        await Assert.That(rows.Any(b => b.Id == dismissed.Id)).IsTrue();
        await Assert.That(rows.Any(b => b.Id == inboxRow.Id)).IsFalse();
    }

    [Test]
    public async Task ListBankTransactions_all_filter_returns_inbox_matched_and_dismissed_rows()
    {
        using var client = Factory.CreateClient();
        var account = await CreateAccountAsync(client, "Checking-LIST-All");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: "NL25RABO0BTX02040",
            accountId: account.Id
        );
        var matched = await CreateBankTransactionAsync(
            client,
            bankAccount.Id,
            new DateOnly(2026, 5, 17),
            42L,
            "All-filter matched"
        );
        var counter = await CreateAccountAsync(client, "Income-LIST-All");
        await CreateJournalEntryForBankTransactionAsync(
            client,
            matched.Id,
            account.Id,
            counter.Id,
            42L
        );

        var dismissed = await CreateOwnedBankTransactionAsync(client, "NL25RABO0BTX02041", -50L);
        using var dismiss = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{dismissed.Id}/dismiss", UriKind.Relative),
            new DismissBankTransactionRequestDto("All-filter dismissed")
        );
        await Assert.That(dismiss.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var inbox = await CreateOwnedBankTransactionAsync(client, "NL25RABO0BTX02042", -60L);

        var rows = await ListBankTransactionsAsync(client, "All");
        await Assert.That(rows.Any(b => b.Id == matched.Id)).IsTrue();
        await Assert.That(rows.Any(b => b.Id == dismissed.Id)).IsTrue();
        await Assert.That(rows.Any(b => b.Id == inbox.Id)).IsTrue();
    }

    [Test]
    public async Task ListBankTransactions_pagination_applies_take_limit()
    {
        using var client = Factory.CreateClient();
        await CreateOwnedBankTransactionAsync(client, "NL26RABO0BTX02050", -1L);
        await CreateOwnedBankTransactionAsync(client, "NL26RABO0BTX02051", -2L);
        await CreateOwnedBankTransactionAsync(client, "NL26RABO0BTX02052", -3L);

        using var response = await client.GetAsync(
            new Uri("/api/bank-transactions?filter=Inbox&take=2", UriKind.Relative)
        );
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<IReadOnlyList<BankTransactionDto>>();
        await Assert.That(rows!.Count).IsLessThanOrEqualTo(2);
    }

    [Test]
    public async Task ListBankTransactions_pagination_skip_advances_past_first_page()
    {
        using var client = Factory.CreateClient();
        var first = await CreateOwnedBankTransactionAsync(
            client,
            "NL27RABO0BTX02060",
            -10L,
            new DateOnly(2020, 1, 1)
        );
        var second = await CreateOwnedBankTransactionAsync(
            client,
            "NL27RABO0BTX02061",
            -11L,
            new DateOnly(2020, 1, 2)
        );

        using var pageOne = await client.GetAsync(
            new Uri("/api/bank-transactions?filter=Inbox&skip=0&take=1", UriKind.Relative)
        );
        var pageOneRows = await pageOne.Content.ReadFromJsonAsync<
            IReadOnlyList<BankTransactionDto>
        >();
        using var pageTwo = await client.GetAsync(
            new Uri("/api/bank-transactions?filter=Inbox&skip=1&take=200", UriKind.Relative)
        );
        var pageTwoRows = await pageTwo.Content.ReadFromJsonAsync<
            IReadOnlyList<BankTransactionDto>
        >();

        // The two pages must not contain the same row by ID (they're disjoint).
        var overlap = pageOneRows!.Select(r => r.Id).Intersect(pageTwoRows!.Select(r => r.Id));
        await Assert.That(overlap).IsEmpty();
    }

    [Test]
    public async Task ListBankTransactions_take_above_max_returns_400()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/bank-transactions?take=201", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListBankTransactions_negative_skip_returns_400()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/bank-transactions?skip=-1", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ListBankTransactions_unknown_filter_returns_400()
    {
        using var client = Factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/bank-transactions?filter=Bogus", UriKind.Relative)
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CategorizeBankTransaction_happy_path_returns_journal_entry()
    {
        using var client = Factory.CreateClient();
        var btx = await CreateOwnedBankTransactionAsync(client, "NL30RABO0BTX03000", -4200L);
        var counterparty = await CreateCounterpartyAsync(client, "AH-API-cat");
        var counterAccount = await CreateExpenseAccountAsync(client, "Groceries-API-cat");

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/categorize", UriKind.Relative),
            new
            {
                CounterpartyId = counterparty.Id,
                NewCounterparty = (object?)null,
                Date = btx.BookingDate,
                Description = "Categorised AH",
                Lines = new[]
                {
                    new
                    {
                        AccountId = counterAccount.Id,
                        Amount = 4200L,
                        Description = (string?)null,
                    },
                },
            }
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JournalEntryDto>();
        await Assert.That(dto!.BankTransactionId).IsEqualTo(btx.Id);
        await Assert.That(dto.Lines).Count().IsEqualTo(2);
        await Assert.That(dto.Lines.Sum(l => l.Amount)).IsEqualTo(0L);
    }

    [Test]
    public async Task CategorizeBankTransaction_self_transfer_with_null_counterparty_returns_ok()
    {
        using var client = Factory.CreateClient();
        var btx = await CreateOwnedBankTransactionAsync(client, "NL30RABO0BTX03100", -2500L);
        var savings = await CreateAccountAsync(client, "Savings-API-self");

        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/categorize", UriKind.Relative),
            new
            {
                CounterpartyId = (Guid?)null,
                NewCounterparty = (object?)null,
                Date = btx.BookingDate,
                Description = "Transfer to savings",
                Lines = new[]
                {
                    new
                    {
                        AccountId = savings.Id,
                        Amount = 2500L,
                        Description = (string?)null,
                    },
                },
            }
        );

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JournalEntryDto>();
        await Assert.That(dto!.BankTransactionId).IsEqualTo(btx.Id);
        await Assert.That(dto.CounterpartyId).IsNull();
        await Assert.That(dto.Lines).Count().IsEqualTo(2);
        await Assert.That(dto.Lines.Sum(l => l.Amount)).IsEqualTo(0L);
    }

    [Test]
    public async Task CategorizeBankTransaction_already_categorised_returns_409()
    {
        using var client = Factory.CreateClient();
        var btx = await CreateOwnedBankTransactionAsync(client, "NL30RABO0BTX03001", -100L);
        var counterparty = await CreateCounterpartyAsync(client, "AH-API-conflict");
        var counterAccount = await CreateExpenseAccountAsync(client, "Groceries-API-conflict");

        var body = new
        {
            CounterpartyId = counterparty.Id,
            NewCounterparty = (object?)null,
            Date = btx.BookingDate,
            Description = (string?)null,
            Lines = new[]
            {
                new
                {
                    AccountId = counterAccount.Id,
                    Amount = 100L,
                    Description = (string?)null,
                },
            },
        };

        using var first = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/categorize", UriKind.Relative),
            body
        );
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);

        using var second = await client.PostAsJsonAsync(
            new Uri($"/api/bank-transactions/{btx.Id}/categorize", UriKind.Relative),
            body
        );
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    private static async Task<AccountDto> CreateExpenseAccountAsync(HttpClient client, string name)
    {
        var req = new CreateAccountRequestDto($"{name}-{Guid.NewGuid():N}", "Expense", "EUR");
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AccountDto>();
        return dto!;
    }

    private static async Task<IReadOnlyList<BankTransactionDto>> ListBankTransactionsAsync(
        HttpClient client,
        string? filter = null
    )
    {
        var query = filter is null ? string.Empty : $"?filter={filter}";
        using var response = await client.GetAsync(
            new Uri($"/api/bank-transactions{query}", UriKind.Relative)
        );
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<IReadOnlyList<BankTransactionDto>>();
        return rows!;
    }

    private static async Task<BankTransactionDto> CreateBankTransactionAsync(
        HttpClient client,
        Guid bankAccountId,
        DateOnly bookingDate,
        long amount,
        string description
    )
    {
        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccountId,
            BookingDate: bookingDate,
            Amount: amount,
            CurrencyCode: "EUR",
            Description: description
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<BankTransactionDto>();
        return dto!;
    }

    private static async Task<Guid> CreateJournalEntryForBankTransactionAsync(
        HttpClient client,
        Guid bankTransactionId,
        Guid bankSideAccountId,
        Guid counterAccountId,
        long bankSideAmount
    )
    {
        var request = new
        {
            Date = new DateOnly(2026, 5, 17),
            Description = "Linked-from-BTX",
            BankTransactionId = bankTransactionId,
            CounterpartyId = (Guid?)null,
            Lines = new[]
            {
                new
                {
                    AccountId = bankSideAccountId,
                    Amount = bankSideAmount,
                    Description = (string?)null,
                },
                new
                {
                    AccountId = counterAccountId,
                    Amount = -bankSideAmount,
                    Description = (string?)null,
                },
            },
        };
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/journal-entries", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<JournalEntryIdDto>();
        return dto!.Id;
    }

    private static async Task<BankTransactionDto> CreateOwnedBankTransactionAsync(
        HttpClient client,
        string iban,
        long amount,
        DateOnly? bookingDate = null
    )
    {
        var account = await CreateAccountAsync(client, $"Checking-{iban}");
        var bankAccount = await CreateBankAccountForAccountAsync(
            client,
            iban: iban,
            accountId: account.Id
        );
        var request = new CreateBankTransactionRequestDto(
            BankAccountId: bankAccount.Id,
            BookingDate: bookingDate ?? new DateOnly(2026, 5, 17),
            Amount: amount,
            CurrencyCode: "EUR",
            Description: $"Dismissal-fixture {iban} {amount}"
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/bank-transactions", UriKind.Relative),
            request
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<BankTransactionDto>();
        return dto!;
    }

    private static async Task<AccountDto> CreateAccountAsync(HttpClient client, string name)
    {
        var req = new CreateAccountRequestDto(name, "Asset", "EUR");
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AccountDto>();
        return dto!;
    }

    private static async Task<CounterpartyDto> CreateCounterpartyAsync(
        HttpClient client,
        string name
    )
    {
        var req = new CreateCounterpartyRequestDto(name);
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/counterparties", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<CounterpartyDto>();
        return dto!;
    }

    private static async Task<BankAccountDto> CreateBankAccountForAccountAsync(
        HttpClient client,
        string iban,
        Guid accountId
    )
    {
        var req = new CreateBankAccountRequestDto(
            Iban: iban,
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: "EUR",
            AccountId: accountId,
            CounterpartyId: null
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/bank-accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<BankAccountDto>();
        return dto!;
    }

    private static async Task<BankAccountDto> CreateBankAccountForCounterpartyAsync(
        HttpClient client,
        string iban,
        Guid counterpartyId
    )
    {
        var req = new CreateBankAccountRequestDto(
            Iban: iban,
            AccountNumber: null,
            Bic: null,
            BankName: null,
            AccountHolderName: null,
            CurrencyCode: "EUR",
            AccountId: null,
            CounterpartyId: counterpartyId
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/bank-accounts", UriKind.Relative),
            req
        );
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<BankAccountDto>();
        return dto!;
    }
}

internal sealed record BankTransactionDto(
    Guid Id,
    Guid BankAccountId,
    DateOnly BookingDate,
    MoneyDto Money,
    string Description,
    string? CounterpartyName,
    string? CounterpartyAccountNumber,
    DateOnly? ValueDate,
    string? Reference,
    string? MandateId,
    string? SepaCreditorId,
    long? ForeignAmount,
    string? ForeignCurrencyCode,
    decimal? ExchangeRate,
    string? ImporterKey,
    Guid? JournalEntryId,
    DateTime? DismissedAt,
    string? DismissedReason,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

internal sealed record MoneyDto(long Amount, string CurrencyCode);

internal sealed record CreateBankTransactionRequestDto(
    Guid BankAccountId,
    DateOnly BookingDate,
    long Amount,
    string CurrencyCode,
    string Description,
    string? CounterpartyName = null,
    string? CounterpartyAccountNumber = null
);

internal sealed record DismissBankTransactionRequestDto(string Reason);

internal sealed record JournalEntryIdDto(Guid Id);
