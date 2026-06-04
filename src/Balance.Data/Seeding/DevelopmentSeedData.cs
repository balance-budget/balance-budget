using System.Security.Cryptography;
using System.Text;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Seeding;

/// <summary>
/// The single source of all <see cref="DevelopmentSeedGraph"/> sample data. Builds a realistic,
/// fully-balanced EUR ledger anchored to "today" so time-relative views (month-to-date, the current
/// reporting period) always have data. Pure construction only — persistence and the
/// wipe/refresh lifecycle live in <see cref="DevelopmentDataSeeder"/>. See ADR-0024.
/// </summary>
internal static class DevelopmentSeedData
{
    public static DevelopmentSeedGraph Build(DateOnly today) => new Builder(today).Build();

    private readonly record struct LineSpec(
        AccountId Account,
        long Amount,
        ReconciliationStatus Status
    );

    private sealed class Builder
    {
        private static readonly CurrencyCode Eur = new("EUR");

        private readonly DateOnly _today;
        private readonly DateTime _structuralCreatedAt;

        private readonly List<Account> _accounts = [];
        private readonly List<Counterparty> _counterparties = [];
        private readonly List<BankAccount> _bankAccounts = [];
        private readonly List<JournalEntry> _journalEntries = [];
        private readonly List<BankTransaction> _bankTransactions = [];

        // Disambiguates RawSource so the (BankAccountId, RowHash) unique index never collides,
        // even for two grocery runs of the same amount in the same month.
        private int _sequence;

        public Builder(DateOnly today)
        {
            _today = today;
            _structuralCreatedAt = Utc(FirstOfMonth(today).AddMonths(-12));
        }

        public DevelopmentSeedGraph Build()
        {
            // Chart of accounts: non-postable parents (ADR-0022) with postable leaves, all EUR.
            var checking = Leaf("1100", "Checking", AccountType.Asset);
            var savings = Leaf("1200", "Savings A", AccountType.Asset);

            var creditCard = Leaf("2100", "Credit Card", AccountType.Liability);
            var mortgage = Leaf("2200", "Mortgage", AccountType.Liability);

            var salary = Leaf("4100", "Salary", AccountType.Income);
            var interest = Leaf("4200", "Interest Received", AccountType.Income);
            var taxReturn = Leaf("4300", "Tax Return", AccountType.Income);

            var housing = Branch("5100", "Housing", AccountType.Expense);
            var taxes = Leaf("5110", "Taxes", AccountType.Expense, housing);
            var housingInsurance = Leaf("5120", "Insurance", AccountType.Expense, housing);
            var maintenance = Leaf("5130", "Maintenance", AccountType.Expense, housing);

            var utilities = Branch("5200", "Utilities", AccountType.Expense);
            var energy = Leaf("5210", "Energy", AccountType.Expense, utilities);
            var water = Leaf("5220", "Water", AccountType.Expense, utilities);
            var internetAndPhone = Leaf("5230", "Internet & Phone", AccountType.Expense, utilities);

            var household = Branch("5300", "Household", AccountType.Expense);
            var groceries = Leaf("5310", "Groceries", AccountType.Expense, household);

            var transport = Leaf("5400", "Transport", AccountType.Expense);
            var car = Leaf("5410", "Car", AccountType.Expense, transport);
            var publicTransport = Leaf("5420", "Public Transport", AccountType.Expense, transport);
            var bike = Leaf("5430", "Bike", AccountType.Expense, transport);

            var health = Leaf("5500", "Health", AccountType.Expense);
            var healthInsurance = Leaf("5510", "Insurance", AccountType.Expense, health);
            var medical = Leaf("5520", "Medical Expenses", AccountType.Expense, health);
            var sports = Leaf("5530", "Sports", AccountType.Expense, health);

            var lifestyle = Leaf("5700", "Lifestyle", AccountType.Expense);
            var eatingInAndOut = Leaf("5710", "Eating In & Out", AccountType.Expense, lifestyle);
            var holidays = Leaf("5720", "Holidays", AccountType.Expense, health);
            var shopping = Leaf("5730", "Shopping", AccountType.Expense, health);
            var activities = Leaf("5740", "Activities", AccountType.Expense, health);
            var subscriptions = Leaf("5750", "Subscriptions", AccountType.Expense, health);

            var misc = Leaf("5800", "Miscellaneous", AccountType.Expense);
            var financial = Leaf("5810", "Financial", AccountType.Expense, misc);
            var education = Leaf("5820", "Education & Training", AccountType.Expense, misc);

            // Counterparties.
            var employer = Counterparty("Acme Corp");
            var supermarket = Counterparty("Albert Heijn");
            var utilityCo = Counterparty("Vattenfall");
            var cafe = Counterparty("Café Central");
            var lender = Counterparty("Big Bank");
            var transitCo = Counterparty("NS");

            // Bank accounts (your side) linked to the postable asset leaves.
            var checkingBank = BankAccount(checking, BankAccountType.Current, "NL01BALA0000000001");
            var savingsBank = BankAccount(savings, BankAccountType.Savings, "NL01BALA0000000002");

            // Trailing 12 months of recurring activity. The current month (m == 0) is naturally
            // truncated to <= today by OnDay, so month-to-date views are always partially filled.
            for (var m = 11; m >= 0; m--)
            {
                var monthStart = FirstOfMonth(_today).AddMonths(-m);
                var reconciled = m >= 10; // oldest months demonstrate the frozen Reconciled state.

                OnDay(
                    monthStart,
                    1,
                    d =>
                        Categorised(
                            checkingBank,
                            checking,
                            mortgage,
                            lender,
                            -120_000,
                            "Monthly payment",
                            d
                        )
                );
                OnDay(
                    monthStart,
                    4,
                    d =>
                        Categorised(
                            checkingBank,
                            checking,
                            energy,
                            utilityCo,
                            -8_000,
                            "Utilities",
                            d
                        )
                );
                OnDay(
                    monthStart,
                    6,
                    d =>
                        Categorised(
                            checkingBank,
                            checking,
                            groceries,
                            supermarket,
                            -5_200,
                            "Groceries",
                            d
                        )
                );
                OnDay(
                    monthStart,
                    14,
                    d =>
                        Categorised(
                            checkingBank,
                            checking,
                            groceries,
                            supermarket,
                            -4_810,
                            "Groceries",
                            d
                        )
                );
                OnDay(
                    monthStart,
                    22,
                    d =>
                        Categorised(
                            checkingBank,
                            checking,
                            groceries,
                            supermarket,
                            -6_390,
                            "Groceries",
                            d
                        )
                );
                OnDay(
                    monthStart,
                    10,
                    d =>
                        Categorised(
                            checkingBank,
                            checking,
                            eatingInAndOut,
                            cafe,
                            -2_750,
                            "Dinner",
                            d
                        )
                );

                // Cash entry: a real posting with no imported BankTransaction — stays Uncleared.
                OnDay(
                    monthStart,
                    17,
                    d => Cash(checking, publicTransport, transitCo, -1_990, "Train ticket", d)
                );

                // Income, attached on import (debit-positive on the asset side).
                OnDay(
                    monthStart,
                    25,
                    d =>
                        Categorised(
                            checkingBank,
                            checking,
                            salary,
                            employer,
                            250_000,
                            "Monthly salary",
                            d,
                            reconciled
                        )
                );

                // Self-transfer: both sibling bank rows present, both legs Cleared.
                OnDay(
                    monthStart,
                    28,
                    d => SelfTransfer(checkingBank, checking, savingsBank, savings, 100_000, d)
                );

                // Quarterly savings interest — a cash posting on the savings side.
                if (monthStart.Month % 3 == 0)
                    OnDay(
                        monthStart,
                        2,
                        d => Cash(savings, interest, null, 250, "Savings interest", d)
                    );
            }

            // Current-month Inbox: un-actioned bank rows (no JournalEntry) plus one Dismissed row.
            Inbox(checkingBank, _today, -3_450, "Bol.com order", "Bol.com");
            Inbox(checkingBank, _today.AddDays(-2), -1_280, "Spotify", "Spotify AB");
            Dismissed(
                checkingBank,
                _today.AddDays(-1),
                -100,
                "Bank verification micro-charge",
                "Test transaction"
            );

            return new DevelopmentSeedGraph
            {
                Accounts = _accounts,
                Counterparties = _counterparties,
                BankAccounts = _bankAccounts,
                JournalEntries = _journalEntries,
                BankTransactions = _bankTransactions,
            };
        }

        // A categorised bank row: a balanced entry whose bank-side line is Cleared (or Reconciled),
        // plus the imported BankTransaction attached to it. The bank-tx amount equals the bank-side
        // line amount under the deliberate raw-equality match (ADR-0013).
        private void Categorised(
            BankAccount bank,
            Account bankSide,
            Account contra,
            Counterparty counterparty,
            long bankSideAmount,
            string description,
            DateOnly date,
            bool reconciled = false
        )
        {
            var status = reconciled
                ? ReconciliationStatus.Reconciled
                : ReconciliationStatus.Cleared;
            var entry = AddEntry(
                date,
                description,
                counterparty.Id,
                new LineSpec(bankSide.Id, bankSideAmount, status),
                new LineSpec(contra.Id, -bankSideAmount, ReconciliationStatus.Uncleared)
            );
            AddBankTransaction(
                bank.Id,
                date,
                bankSideAmount,
                description,
                counterparty.Name,
                entry.Id
            );
        }

        // A cash entry: no imported BankTransaction, so both lines stay Uncleared.
        private void Cash(
            Account assetSide,
            Account contra,
            Counterparty? counterparty,
            long assetSideAmount,
            string description,
            DateOnly date
        ) =>
            AddEntry(
                date,
                description,
                counterparty?.Id,
                new LineSpec(assetSide.Id, assetSideAmount, ReconciliationStatus.Uncleared),
                new LineSpec(contra.Id, -assetSideAmount, ReconciliationStatus.Uncleared)
            );

        // A self-transfer between two own accounts, with both statement rows imported and attached.
        private void SelfTransfer(
            BankAccount fromBank,
            Account from,
            BankAccount toBank,
            Account to,
            long amount,
            DateOnly date
        )
        {
            var entry = AddEntry(
                date,
                "Transfer to savings",
                counterpartyId: null,
                new LineSpec(from.Id, -amount, ReconciliationStatus.Cleared),
                new LineSpec(to.Id, amount, ReconciliationStatus.Cleared)
            );
            AddBankTransaction(
                fromBank.Id,
                date,
                -amount,
                "Transfer to savings",
                counterpartyName: null,
                entry.Id
            );
            AddBankTransaction(
                toBank.Id,
                date,
                amount,
                "Transfer from checking",
                counterpartyName: null,
                entry.Id
            );
        }

        private void Inbox(
            BankAccount bank,
            DateOnly date,
            long amount,
            string description,
            string counterpartyName
        )
        {
            if (date <= _today)
                AddBankTransaction(
                    bank.Id,
                    date,
                    amount,
                    description,
                    counterpartyName,
                    journalEntryId: null
                );
        }

        private void Dismissed(
            BankAccount bank,
            DateOnly date,
            long amount,
            string description,
            string reason
        )
        {
            if (date <= _today)
                AddBankTransaction(
                    bank.Id,
                    date,
                    amount,
                    description,
                    counterpartyName: null,
                    journalEntryId: null,
                    dismissed: (Utc(date), reason)
                );
        }

        private JournalEntry AddEntry(
            DateOnly date,
            string description,
            CounterpartyId? counterpartyId,
            LineSpec primary,
            LineSpec contra
        )
        {
            var id = new JournalEntryId(Guid.CreateVersion7());
            var at = Utc(date);
            var entry = new JournalEntry
            {
                Id = id,
                Date = date,
                Description = description,
                CounterpartyId = counterpartyId,
                CreatedAt = at,
                UpdatedAt = at,
            };
            entry.Lines.Add(AddLine(id, primary, at));
            entry.Lines.Add(AddLine(id, contra, at));
            _journalEntries.Add(entry);
            return entry;
        }

        private static JournalLine AddLine(JournalEntryId entryId, LineSpec spec, DateTime at) =>
            new()
            {
                Id = new JournalLineId(Guid.CreateVersion7()),
                JournalEntryId = entryId,
                AccountId = spec.Account,
                Amount = spec.Amount,
                ReconciliationStatus = spec.Status,
                CreatedAt = at,
                UpdatedAt = at,
            };

        private void AddBankTransaction(
            BankAccountId bankAccountId,
            DateOnly date,
            long amount,
            string description,
            string? counterpartyName,
            JournalEntryId? journalEntryId,
            (DateTime At, string Reason)? dismissed = null
        )
        {
            var raw = $"SEED;{date:yyyy-MM-dd};{amount};{description};{_sequence++}";
            var at = Utc(date);
            _bankTransactions.Add(
                new BankTransaction
                {
                    Id = new BankTransactionId(Guid.CreateVersion7()),
                    BankAccountId = bankAccountId,
                    BookingDate = date,
                    Money = new Money(amount, Eur),
                    Description = description,
                    CounterpartyName = counterpartyName,
                    RawSource = raw,
                    RowHash = Hash(raw),
                    JournalEntryId = journalEntryId,
                    DismissedAt = dismissed?.At,
                    DismissedReason = dismissed?.Reason,
                    CreatedAt = at,
                    UpdatedAt = at,
                }
            );
        }

        private Account Branch(
            string code,
            string name,
            AccountType type,
            Account? parent = null
        ) => AddAccount(code, name, type, postable: false, parent: parent?.Id);

        private Account Leaf(string code, string name, AccountType type, Account? parent = null) =>
            AddAccount(code, name, type, postable: true, parent?.Id);

        private Account AddAccount(
            string code,
            string name,
            AccountType type,
            bool postable,
            AccountId? parent
        )
        {
            var account = new Account
            {
                Id = new AccountId(Guid.CreateVersion7()),
                Code = code,
                Name = name,
                AccountType = type,
                CurrencyCode = Eur,
                IsPostable = postable,
                ParentAccountId = parent,
                CreatedAt = _structuralCreatedAt,
                UpdatedAt = _structuralCreatedAt,
            };
            _accounts.Add(account);
            return account;
        }

        private Counterparty Counterparty(string name)
        {
            var counterparty = new Counterparty
            {
                Id = new CounterpartyId(Guid.CreateVersion7()),
                Name = name,
                CreatedAt = _structuralCreatedAt,
                UpdatedAt = _structuralCreatedAt,
            };
            _counterparties.Add(counterparty);
            return counterparty;
        }

        private BankAccount BankAccount(Account owner, BankAccountType type, string iban)
        {
            var bankAccount = new BankAccount
            {
                Id = new BankAccountId(Guid.CreateVersion7()),
                Type = type,
                Iban = iban,
                CurrencyCode = Eur,
                AccountId = owner.Id,
                BankName = "ABN AMRO",
                AccountHolderName = "Developer",
                CreatedAt = _structuralCreatedAt,
                UpdatedAt = _structuralCreatedAt,
            };
            _bankAccounts.Add(bankAccount);
            return bankAccount;
        }

        private void OnDay(DateOnly monthStart, int day, Action<DateOnly> action)
        {
            var date = monthStart.AddDays(day - 1);
            if (date <= _today)
                action(date);
        }

        private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

        private static DateTime Utc(DateOnly date) =>
            new(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);

        // 64-char uppercase hex, matching the fixed-length CHAR(64) RowHash column.
        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
