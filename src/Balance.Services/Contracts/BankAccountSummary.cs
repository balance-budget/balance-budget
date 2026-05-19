namespace Balance.Services.Contracts;

public sealed record BankAccountSummary(
    string? Iban,
    string? AccountNumber,
    string? Bic,
    string? BankName
);
