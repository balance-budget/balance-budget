using Balance.Integration.Stater.Models;

namespace Balance.Integration.Stater.Contracts;

internal interface IStaterStatementParser
{
    // Null when the lines carry no recognizable header account number.
    StaterStatement? Parse(IReadOnlyList<StaterTextLine> lines);
}
