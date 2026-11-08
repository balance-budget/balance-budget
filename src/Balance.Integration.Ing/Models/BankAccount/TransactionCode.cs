using CsvHelper;

namespace Balance.Integration.Ing.Models.BankAccount;

/// <remarks>
/// Names are taken from Dutch and English descriptions used by ING
/// </remarks>
internal enum TransactionCode
{
    /// <summary>
    /// Acceptgiro
    /// </summary>
    [CsvEnumName("AC")]
    AcceptGiro,

    /// <summary>
    /// Betaalautomaat / Payment terminal
    /// </summary>
    [CsvEnumName("BA")]
    PaymentTerminal,

    /// <summary>
    /// Diversen / Various
    /// </summary>
    [CsvEnumName("DV")]
    Various,

    /// <summary>
    /// Filiaalboeking / Branch transfer
    /// </summary>
    [CsvEnumName("FL")]
    BranchTransfer,

    /// <summary>
    /// Telefonisch bankieren (Girofoon)
    /// </summary>
    [CsvEnumName("GF")]
    GiroPhone,

    /// <summary>
    /// Geldautomaat / Cash machine (Giromaat)
    /// </summary>
    [CsvEnumName("GM")]
    CashMachine,

    /// <summary>
    /// Online bankieren / Online banking (Girotel)
    /// </summary>
    [CsvEnumName("GT")]
    OnlineBanking,

    /// <summary>
    /// Incasso / SEPA direct debit
    /// </summary>
    [CsvEnumName("IC")]
    SepaDirectDebit,

    /// <summary>
    /// iDEAL
    /// </summary>
    [CsvEnumName("ID")]
    Ideal,

    /// <summary>
    /// Overschrijving / Transfer
    /// </summary>
    [CsvEnumName("OV")]
    Transfer,

    /// <summary>
    /// Opname kantoor (postkantoor)
    /// </summary>
    [CsvEnumName("PK")]
    BranchWithdrawal,

    /// <summary>
    /// Periodieke overschrijving / Recurring transfer
    /// </summary>
    [CsvEnumName("PO")]
    RecurringTransfer,

    /// <summary>
    /// Storting / Deposit
    /// </summary>
    [CsvEnumName("ST")]
    Deposit,

    /// <summary>
    /// Verzamelbetaling / Batch payment
    /// </summary>
    [CsvEnumName("VZ")]
    BatchPayment,

    /// <summary>
    /// iDEAL | Wero
    /// </summary>
    [CsvEnumName("IW")]
    IdealWero,

    /// <summary>
    /// Wero
    /// </summary>
    [CsvEnumName("WERO")]
    Wero,

    /// <summary>
    /// Vreemde valuta: Cheque
    /// </summary>
    [CsvEnumName("CHK")]
    ForeignCurrencyCheque,

    /// <summary>
    /// Vreemde valuta: Overboeking
    /// </summary>
    [CsvEnumName("TRF")]
    ForeignCurrencyTransfer,
}

internal static class TransactionCodes
{
    /// <summary>
    /// The code as ING writes it, for metadata and round-tripping. Mirrors the
    /// <see cref="CsvEnumNameAttribute" /> names, which CsvHelper only reads in the parse
    /// direction; <c>TransactionCodeTests</c> guards the two against drift.
    /// </summary>
    public static string ToCode(this TransactionCode code) =>
        code switch
        {
            TransactionCode.AcceptGiro => "AC",
            TransactionCode.PaymentTerminal => "BA",
            TransactionCode.Various => "DV",
            TransactionCode.BranchTransfer => "FL",
            TransactionCode.GiroPhone => "GF",
            TransactionCode.CashMachine => "GM",
            TransactionCode.OnlineBanking => "GT",
            TransactionCode.SepaDirectDebit => "IC",
            TransactionCode.Ideal => "ID",
            TransactionCode.Transfer => "OV",
            TransactionCode.BranchWithdrawal => "PK",
            TransactionCode.RecurringTransfer => "PO",
            TransactionCode.Deposit => "ST",
            TransactionCode.BatchPayment => "VZ",
            TransactionCode.IdealWero => "IW",
            TransactionCode.Wero => "WERO",
            TransactionCode.ForeignCurrencyCheque => "CHK",
            TransactionCode.ForeignCurrencyTransfer => "TRF",
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
        };
}
