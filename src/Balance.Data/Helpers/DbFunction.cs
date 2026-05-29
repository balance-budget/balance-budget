using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Balance.Data.Helpers;

public static class DbFunction
{
    public static bool CaseInsensitiveLike(string matchExpression, string pattern) =>
        throw new InvalidOperationException(
            CoreStrings.FunctionOnClient(nameof(CaseInsensitiveLike))
        );
}
