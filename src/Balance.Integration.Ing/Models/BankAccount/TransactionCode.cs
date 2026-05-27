using CsvHelper.Configuration.Attributes;

namespace Balance.Integration.Ing.Models.BankAccount;

/// <remarks>
/// Names are taken from Dutch and English descriptions used by ING
/// </remarks>
public enum TransactionCode
{
    /// <summary>
    /// Acceptgiro
    /// </summary>
    [Name("AC")]
    AcceptGiro,

    /// <summary>
    /// Betaalautomaat / Payment terminal
    /// </summary>
    [Name("BA")]
    PaymentTerminal,

    /// <summary>
    /// Diversen / Various
    /// </summary>
    [Name("DV")]
    Various,

    /// <summary>
    /// Filiaalboeking / Branch transfer
    /// </summary>
    [Name("FL")]
    BranchTransfer,

    /// <summary>
    /// Telefonisch bankieren (Girofoon)
    /// </summary>
    [Name("GF")]
    GiroPhone,

    /// <summary>
    /// Geldautomaat / Cash machine (Giromaat)
    /// </summary>
    [Name("GM")]
    CashMachine,

    /// <summary>
    /// Online bankieren / Online banking (Girotel)
    /// </summary>
    [Name("GT")]
    OnlineBanking,

    /// <summary>
    /// Incasso / SEPA direct debit
    /// </summary>
    [Name("IC")]
    SepaDirectDebit,

    /// <summary>
    /// iDEAL
    /// </summary>
    [Name("ID")]
    Ideal,

    /// <summary>
    /// Overschrijving / Transfer
    /// </summary>
    [Name("OV")]
    Transfer,

    /// <summary>
    /// Opname kantoor (postkantoor)
    /// </summary>
    [Name("PK")]
    BranchWithdrawal,

    /// <summary>
    /// Periodieke overschrijving / Recurring transfer
    /// </summary>
    [Name("PO")]
    RecurringTransfer,

    /// <summary>
    /// Storting / Deposit
    /// </summary>
    [Name("ST")]
    Deposit,

    /// <summary>
    /// Verzamelbetaling / Batch payment
    /// </summary>
    [Name("VZ")]
    BatchPayment,

    /// <summary>
    /// iDEAL | Wero
    /// </summary>
    [Name("IW")]
    IdealWero,

    /// <summary>
    /// Wero
    /// </summary>
    [Name("WERO")]
    Wero,

    /// <summary>
    /// Vreemde valuta: Cheque
    /// </summary>
    [Name("CHK")]
    ForeignCurrencyCheque,

    /// <summary>
    /// Vreemde valuta: Overboeking
    /// </summary>
    [Name("TRF")]
    ForeignCurrencyTransfer,
}
