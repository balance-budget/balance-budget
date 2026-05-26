using Balance.Data.Entities;

namespace Balance.Services.Contracts;

/// <summary>
/// Re-extracts the promoted columns + metadata from a stored <c>BankTransaction.RawSource</c>.
/// Used by the one-shot re-extraction backfill (issue #89) so historical rows can be brought
/// up to the live extractor's parsing without re-importing the original statement file.
///
/// This interface and its implementations are throwaway scaffolding for the one-shot backfill;
/// a follow-up PR removes the backfill and this contract once the task has been run against the
/// production database.
/// </summary>
public interface IBankTransactionReExtractor
{
    /// <summary>
    /// Matches <c>BankTransaction.ImporterKey</c>; the backfill dispatches each row to the
    /// re-extractor with the matching key.
    /// </summary>
    string ImporterKey { get; }

    Task<Result<BankTransactionReExtraction>> ReExtractAsync(
        string rawSource,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// The promoted columns + metadata produced by re-parsing a single <c>BankTransaction.RawSource</c>.
/// Identifying / amount / description fields aren't returned — those are already on the existing row
/// and the backfill never overwrites them.
/// </summary>
public sealed record BankTransactionReExtraction(
    DateOnly? ValueDate,
    string? Reference,
    string? MandateId,
    string? SepaCreditorId,
    long? ForeignAmount,
    string? ForeignCurrencyCode,
    decimal? ExchangeRate,
    IReadOnlyList<BankTransactionMetadataValue> Metadata
);
