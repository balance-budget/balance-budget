using Balance.Data.Entities.Ids;
using FluentValidation;

namespace Balance.Web.Endpoints.JournalLines;

/// <summary>
/// Bulk reassign: re-point every line in <see cref="LineIds"/> to
/// <see cref="TargetAccountId"/>, all-or-nothing. The batch is capped at one register page
/// (<see cref="MaxLines"/>) — selection is page-bound in the UI.
/// </summary>
internal sealed record ReassignJournalLinesRequest(
    IReadOnlyList<JournalLineId> LineIds,
    AccountId TargetAccountId
)
{
    public const int MaxLines = 200;
}

internal sealed class ReassignJournalLinesRequestValidator
    : AbstractValidator<ReassignJournalLinesRequest>
{
    public ReassignJournalLinesRequestValidator()
    {
        RuleFor(x => x.LineIds).NotEmpty();
        RuleFor(x => x.LineIds.Count)
            .LessThanOrEqualTo(ReassignJournalLinesRequest.MaxLines)
            .When(x => x.LineIds is not null);
    }
}
