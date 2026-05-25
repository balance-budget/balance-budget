using Balance.Data.Entities.Ids;
using Balance.Web.Endpoints.BankTransactions;

namespace Balance.Tests.Validation;

internal sealed class CategorizeBankTransactionRequestValidatorTests
{
    private static readonly CategorizeBankTransactionRequestValidator Validator = new();

    [Test]
    public async Task Valid_request_with_existing_counterparty_passes(
        CancellationToken cancellationToken
    )
    {
        var request = ValidRequestWithExistingCounterparty();

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Valid_request_with_null_counterparty_passes_self_transfer(
        CancellationToken cancellationToken
    )
    {
        // The validator does not enforce the CounterpartyId XOR NewCounterparty
        // rule — that lives in the service layer. A self-transfer request
        // (both null) must therefore pass validation.
        var request = ValidRequestWithExistingCounterparty() with
        {
            CounterpartyId = null,
            NewCounterparty = null,
        };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Valid_request_with_new_counterparty_passes(
        CancellationToken cancellationToken
    )
    {
        var request = ValidRequestWithExistingCounterparty() with
        {
            CounterpartyId = null,
            NewCounterparty = new NewCounterpartyRequest("Albert Heijn"),
        };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Default_date_fails(CancellationToken cancellationToken)
    {
        var request = ValidRequestWithExistingCounterparty() with { Date = default };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsFalse();
        await Assert
            .That(
                result.Errors.Any(e =>
                    e.PropertyName == nameof(CategorizeBankTransactionRequest.Date)
                )
            )
            .IsTrue();
    }

    [Test]
    public async Task Description_over_512_chars_fails(CancellationToken cancellationToken)
    {
        var request = ValidRequestWithExistingCounterparty() with
        {
            Description = new string('x', 513),
        };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsFalse();
        await Assert
            .That(
                result.Errors.Any(e =>
                    e.PropertyName == nameof(CategorizeBankTransactionRequest.Description)
                )
            )
            .IsTrue();
    }

    [Test]
    public async Task Description_exactly_512_chars_passes(CancellationToken cancellationToken)
    {
        var request = ValidRequestWithExistingCounterparty() with
        {
            Description = new string('x', 512),
        };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsTrue();
    }

    [Test]
    public async Task Null_lines_fails(CancellationToken cancellationToken)
    {
        var request = ValidRequestWithExistingCounterparty() with { Lines = null! };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsFalse();
        await Assert
            .That(
                result.Errors.Any(e =>
                    e.PropertyName == nameof(CategorizeBankTransactionRequest.Lines)
                )
            )
            .IsTrue();
    }

    [Test]
    public async Task Empty_lines_fails(CancellationToken cancellationToken)
    {
        var request = ValidRequestWithExistingCounterparty() with
        {
            Lines = Array.Empty<CategorizeBankTransactionLineRequest>(),
        };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsFalse();
        await Assert
            .That(
                result.Errors.Any(e =>
                    e.PropertyName == nameof(CategorizeBankTransactionRequest.Lines)
                )
            )
            .IsTrue();
    }

    [Test]
    public async Task Line_with_empty_account_id_fails(CancellationToken cancellationToken)
    {
        var request = ValidRequestWithExistingCounterparty() with
        {
            Lines = new[]
            {
                new CategorizeBankTransactionLineRequest(new AccountId(Guid.Empty), 1000, null),
            },
        };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task Line_with_zero_amount_fails(CancellationToken cancellationToken)
    {
        var request = ValidRequestWithExistingCounterparty() with
        {
            Lines = new[]
            {
                new CategorizeBankTransactionLineRequest(new AccountId(Guid.NewGuid()), 0L, null),
            },
        };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsFalse();
        await Assert
            .That(result.Errors.Any(e => e.ErrorMessage == "Amount must be non-zero."))
            .IsTrue();
    }

    [Test]
    public async Task Line_description_over_512_chars_fails(CancellationToken cancellationToken)
    {
        var request = ValidRequestWithExistingCounterparty() with
        {
            Lines = new[]
            {
                new CategorizeBankTransactionLineRequest(
                    new AccountId(Guid.NewGuid()),
                    1000,
                    new string('y', 513)
                ),
            },
        };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task New_counterparty_with_empty_name_fails(CancellationToken cancellationToken)
    {
        var request = ValidRequestWithExistingCounterparty() with
        {
            CounterpartyId = null,
            NewCounterparty = new NewCounterpartyRequest(string.Empty),
        };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task New_counterparty_with_whitespace_name_fails(
        CancellationToken cancellationToken
    )
    {
        var request = ValidRequestWithExistingCounterparty() with
        {
            CounterpartyId = null,
            NewCounterparty = new NewCounterpartyRequest("   "),
        };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsFalse();
    }

    [Test]
    public async Task New_counterparty_with_name_over_200_chars_fails(
        CancellationToken cancellationToken
    )
    {
        var request = ValidRequestWithExistingCounterparty() with
        {
            CounterpartyId = null,
            NewCounterparty = new NewCounterpartyRequest(new string('a', 201)),
        };

        var result = await Validator.ValidateAsync(request, cancellationToken);

        await Assert.That(result.IsValid).IsFalse();
    }

    private static CategorizeBankTransactionRequest ValidRequestWithExistingCounterparty() =>
        new(
            CounterpartyId: new CounterpartyId(Guid.NewGuid()),
            NewCounterparty: null,
            Date: new DateOnly(2026, 5, 17),
            Description: "AH groceries",
            Lines: new[]
            {
                new CategorizeBankTransactionLineRequest(
                    new AccountId(Guid.NewGuid()),
                    4200,
                    Description: null
                ),
            }
        );
}
