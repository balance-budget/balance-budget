using Balance.Data.Entities.Ids;
using Microsoft.AspNetCore.Identity;

namespace Balance.Data.Entities;

public sealed class BalanceUser : IdentityUser<UserId>
{
    public required string DisplayName { get; set; }
}
