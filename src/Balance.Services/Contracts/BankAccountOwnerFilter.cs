namespace Balance.Services.Contracts;

/// <summary>
/// Owner facet for the BankAccount list view: the user's own BankAccounts
/// (<see cref="Mine"/>, linked to an Account) versus counterparties'
/// (<see cref="Others"/>, linked to a Counterparty). A null filter lists both.
/// </summary>
public enum BankAccountOwnerFilter
{
    /// <summary>BankAccounts linked to one of the user's own Accounts (AccountId set).</summary>
    Mine = 0,

    /// <summary>BankAccounts linked to a Counterparty (CounterpartyId set).</summary>
    Others,
}
