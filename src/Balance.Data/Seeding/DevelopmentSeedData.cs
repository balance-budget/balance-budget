using System.Security.Cryptography;
using System.Text;
using Balance.Data.Configurations;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Seeding;

/// <summary>
/// The single source of all <see cref="DevelopmentSeedGraph"/> sample data. Builds a realistic,
/// fully-balanced EUR ledger anchored to "today" so time-relative views (month-to-date, the current
/// reporting period) always have data. Pure construction only — persistence and the
/// wipe/refresh lifecycle live in <see cref="DevelopmentDataSeeder"/>. See ADR-0021.
/// </summary>
internal static class DevelopmentSeedData
{
    public static DevelopmentSeedGraph Build(DateOnly today) => new Builder(today).Build();

    private readonly record struct LineSpec(
        AccountId Account,
        long Amount,
        ReconciliationStatus Status,
        LoanPartId? LoanPart = null
    );

    /// <summary>One counter-side leg of a seeded loan payment, attributed to its Loan Part.</summary>
    private readonly record struct LoanLineSpec(LoanPart Part, AccountId Account, long Amount);

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
        private readonly List<Loan> _loans = [];
        private readonly List<LoanPart> _loanParts = [];
        private readonly List<LoanPartRatePeriod> _loanPartRatePeriods = [];
        private readonly List<JournalEntryTemplate> _journalEntryTemplates = [];

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
            // Chart of accounts: non-postable parents (ADR-0019) with postable leaves, all EUR.
            var checking = Leaf("1100", "Checking", AccountType.Asset);
            var savings = Leaf("1200", "Savings", AccountType.Asset);

            // Illiquid world: not day-to-day money, so excluded from liquid net worth — together
            // with the mortgage these keep the dev dashboard's two net-worth figures visibly apart.
            var investments = Leaf("1300", "Investments", AccountType.Asset, liquid: false);
            var home = Leaf("1400", "Home", AccountType.Asset, liquid: false);

            // Construction deposit (ADR-0026): undisbursed mortgage money earmarked for building,
            // a plain (non-loan-managed) Illiquid Asset the loan references for its interest offset.
            var constructionDeposit = Leaf(
                "1500",
                "Construction Deposit",
                AccountType.Asset,
                liquid: false
            );

            var creditCard = Leaf("2100", "Credit Card", AccountType.Liability);

            // The mortgage as a Loan subtree (ADR-0025): one non-postable parent, one postable
            // Illiquid leaf per part — an annuity part and an interest-only part with different
            // rates, a typical multi-part mortgage shape.
            var mortgage = AddAccount(
                "2200",
                "Mortgage",
                AccountType.Liability,
                postable: false,
                liquid: false,
                parent: null
            );
            var mortgagePart1 = Leaf(
                "2210",
                "Mortgage · Part 1",
                AccountType.Liability,
                mortgage,
                liquid: false
            );
            var mortgagePart2 = Leaf(
                "2220",
                "Mortgage · Part 2",
                AccountType.Liability,
                mortgage,
                liquid: false
            );

            // Counter-account for revaluations of the illiquid assets (the seeded Opening
            // Balances equity account stays reserved for onboarding entries).
            var unrealizedGains = Leaf("3800", "Unrealized Gains", AccountType.Equity);

            var salary = Leaf("4100", "Salary", AccountType.Income);
            var interest = Leaf("4200", "Interest Received", AccountType.Income);
            var depositInterest = Leaf("4250", "Construction Deposit Interest", AccountType.Income);
            var taxReturn = Leaf("4300", "Tax Return", AccountType.Income);

            var housing = Branch("5100", "Housing", AccountType.Expense);
            var taxes = Leaf("5110", "Taxes", AccountType.Expense, housing);
            var housingInsurance = Leaf("5120", "Insurance", AccountType.Expense, housing);
            var maintenance = Leaf("5130", "Maintenance", AccountType.Expense, housing);
            var mortgageInterest = Leaf("5140", "Mortgage Interest", AccountType.Expense, housing);

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
            var telecom = Counterparty("KPN");
            var healthInsurer = Counterparty("Zilveren Kruis");
            var gym = Counterparty("Basic-Fit");
            var streaming = Counterparty("Netflix");
            var bank = Counterparty("ABN AMRO");
            var taxOffice = Counterparty("Belastingdienst");
            var housingInsurer = Counterparty("Centraal Beheer");
            var handyman = Counterparty("Klusbedrijf Jansen");
            var waterCo = Counterparty("Vitens");
            var garage = Counterparty("Garage Bosch");
            var bikeShop = Counterparty("Swapfiets");
            var dentist = Counterparty("Tandartspraktijk");
            var travelCo = Counterparty("TUI");
            var clothingStore = Counterparty("Zalando");
            var ticketing = Counterparty("Ticketmaster");
            var school = Counterparty("Coursera");
            var onlineStore = Counterparty("Coolblue");
            var creditCardCo = Counterparty("ICS");
            var broker = Counterparty("DeGiro");

            // Bank accounts (your side) linked to the postable asset leaves.
            var checkingBank = BankAccount(checking, BankAccountType.Current, "NL01BALA0000000001");
            var savingsBank = BankAccount(savings, BankAccountType.Savings, "NL01BALA0000000002");

            // The lender's account: monthly debits reference this IBAN as their counterparty
            // account number, which is what resolves the Inbox's loan-payment hint (ADR-0025).
            const string lenderIban = "NL45BIGB0001234567";
            CounterpartyBankAccount(lender, lenderIban);

            // The Loan over the mortgage subtree: an annuity part (€240k at 3.6%, fixed 10y)
            // and an interest-only part (€60k at 2.9%, fixed 5y), both running 28 more years.
            var openingDateForLoan = FirstOfMonth(_today).AddMonths(-12);
            var loan = Loan("Home mortgage", lender, mortgageInterest, mortgage);
            var part1 = LoanPart(
                loan,
                "Part 1 · Annuity",
                LoanRepaymentType.Annuity,
                openingDateForLoan,
                openingDateForLoan.AddYears(28),
                mortgagePart1
            );
            RatePeriod(part1, openingDateForLoan, 3.6m, openingDateForLoan.AddYears(10));
            var part2 = LoanPart(
                loan,
                "Part 2 · Interest-only",
                LoanRepaymentType.InterestOnly,
                openingDateForLoan,
                openingDateForLoan.AddYears(28),
                mortgagePart2
            );
            RatePeriod(part2, openingDateForLoan, 2.9m, openingDateForLoan.AddYears(5));

            // A Construction deposit on the loan (ADR-0026): during the build, interest accrued on
            // the deposit offsets the loan interest, so the loan-payment proposal nets it out.
            loan.ConstructionDepositAccountId = constructionDeposit.Id;
            loan.ConstructionDepositInterestIncomeAccountId = depositInterest.Id;
            loan.ConstructionDepositAnnualRatePercent = 3.6m;

            // Opening balances against the seeded Opening Balances equity account (AccountSeed), dated
            // a year back so every running balance starts from a realistic position. Entered by hand,
            // so both legs are Reconciled and there is no bank import.
            var openingDate = FirstOfMonth(_today).AddMonths(-12);
            Opening(checking, 1_000_000, openingDate); // €10,000.00
            Opening(savings, 2_500_000, openingDate); // €25,000.00
            Opening(investments, 1_500_000, openingDate); // €15,000.00
            Opening(home, 38_000_000, openingDate); // €380,000.00
            Opening(mortgagePart1, -24_000_000, openingDate); // €240,000.00 outstanding
            Opening(mortgagePart2, -6_000_000, openingDate); // €60,000.00 outstanding
            Opening(constructionDeposit, 4_000_000, openingDate); // €40,000.00 still in the deposit

            // Recurring monthly spend on the checking account — one row per category so the whole
            // chart of accounts carries a year of activity. (day, contra account, counterparty, cents).
            (
                int Day,
                Account Contra,
                Counterparty Counterparty,
                long Amount,
                string Description
            )[] monthly =
            [
                (3, internetAndPhone, telecom, -4_500, "Internet & phone"),
                (4, energy, utilityCo, -8_000, "Energy"),
                (5, healthInsurance, healthInsurer, -13_500, "Health insurance"),
                (6, groceries, supermarket, -5_200, "Groceries"),
                (8, sports, gym, -2_500, "Gym membership"),
                (10, eatingInAndOut, cafe, -2_750, "Dinner"),
                (13, subscriptions, streaming, -2_580, "Subscriptions"),
                (14, groceries, supermarket, -4_810, "Groceries"),
                (18, financial, bank, -350, "Account fee"),
                (22, groceries, supermarket, -6_390, "Groceries"),
                (26, investments, broker, -25_000, "Investment contribution"),
            ];

            // Periodic spend (annual / quarterly / occasional), keyed off the calendar month so the
            // year shows seasonality. The tax refund is the lone inflow. (when, day, contra, cp, cents).
            (
                Func<int, bool> When,
                int Day,
                Account Contra,
                Counterparty Counterparty,
                long Amount,
                string Description
            )[] periodic =
            [
                (month => month == 3, 9, taxes, taxOffice, -45_000, "Municipal taxes"),
                (
                    month => month == 1,
                    15,
                    housingInsurance,
                    housingInsurer,
                    -28_000,
                    "Home insurance"
                ),
                (month => month is 6 or 11, 12, maintenance, handyman, -15_000, "Home maintenance"),
                (month => month % 3 == 1, 20, water, waterCo, -3_000, "Water"),
                (month => month % 3 == 2, 16, car, garage, -9_000, "Car service"),
                (month => month == 4, 11, bike, bikeShop, -3_500, "Bike repair"),
                (month => month is 2 or 9, 19, medical, dentist, -4_500, "Dentist"),
                (month => month == 7, 5, holidays, travelCo, -180_000, "Summer holiday"),
                (month => month is 4 or 11, 24, shopping, clothingStore, -12_000, "Clothing"),
                (month => month is 5 or 10, 21, activities, ticketing, -6_000, "Concert tickets"),
                (month => month == 9, 7, education, school, -25_000, "Online course"),
                (month => month == 5, 13, taxReturn, taxOffice, 60_000, "Tax refund"),
            ];

            // The fixed shape of the seeded monthly mortgage payment (a simplification — a real
            // annuity shifts principal and interest over time). Gross interest drives the deposit
            // offset cap; gross total is what the bank would collect with no deposit.
            const long part1Principal = 42_000;
            const long part1Interest = 71_500;
            const long part2Interest = 14_500;
            const long grossInterest = part1Interest + part2Interest;
            const long grossTotal = part1Principal + grossInterest;

            // The Construction deposit balance, tracked alongside the ledger so each month's
            // deposit-interest offset reflects the balance at that month's start (ADR-0026). It opens
            // at €40,000 and tapers as draws fund the build, so the offset visibly shrinks over the year.
            var depositBalance = 4_000_000L;
            const decimal depositRate = 3.6m;

            // Trailing 12 months of recurring activity. The current month (m == 0) is naturally
            // truncated to <= today by OnDay, so month-to-date views are always partially filled.
            for (var m = 11; m >= 0; m--)
            {
                var monthStart = FirstOfMonth(_today).AddMonths(-m);
                var reconciled = m >= 10; // oldest months demonstrate the frozen Reconciled state.

                // The deposit-interest offset for this month, off the balance at month start
                // (the draw below lands later in the month, so it only affects later months).
                var depositOffset = DepositOffset(depositBalance, depositRate, grossInterest);

                // The monthly loan payment in its loan-aware shape (ADR-0025): bank line
                // Cleared, principal on the annuity part, one interest line per part, each
                // counter-line attributed to its Loan Part, plus the construction-deposit
                // interest offset (ADR-0026) netting the gross down to the bank's single debit.
                // The current month's debit stays in the Inbox so the loan-payment hint has
                // something to point at — at its net amount, the figure the bank actually charges.
                if (m > 0)
                {
                    OnDay(
                        monthStart,
                        1,
                        d =>
                            LoanPayment(
                                checkingBank,
                                checking,
                                lender,
                                lenderIban,
                                d,
                                depositInterest,
                                depositOffset,
                                new LoanLineSpec(part1, mortgagePart1.Id, part1Principal),
                                new LoanLineSpec(part1, mortgageInterest.Id, part1Interest),
                                new LoanLineSpec(part2, mortgageInterest.Id, part2Interest)
                            )
                    );
                }
                else
                {
                    OnDay(
                        monthStart,
                        1,
                        d =>
                            Inbox(
                                checkingBank,
                                d,
                                -(grossTotal - depositOffset),
                                "Mortgage payment",
                                lender.Name,
                                lenderIban
                            )
                    );
                }

                foreach (var s in monthly)
                    OnDay(
                        monthStart,
                        s.Day,
                        d =>
                            Categorized(
                                checkingBank,
                                checking,
                                s.Contra,
                                s.Counterparty,
                                s.Amount,
                                s.Description,
                                d
                            )
                    );

                foreach (var p in periodic)
                    if (p.When(monthStart.Month))
                        OnDay(
                            monthStart,
                            p.Day,
                            d =>
                                Categorized(
                                    checkingBank,
                                    checking,
                                    p.Contra,
                                    p.Counterparty,
                                    p.Amount,
                                    p.Description,
                                    d
                                )
                        );

                // Revolving credit card: a purchase on the card (no bank import) and its repayment
                // from checking, so the card balance oscillates around zero across the month.
                OnDay(
                    monthStart,
                    15,
                    d => Cash(shopping, creditCard, onlineStore, 3_900, "Card purchase", d)
                );
                OnDay(
                    monthStart,
                    27,
                    d =>
                        Categorized(
                            checkingBank,
                            checking,
                            creditCard,
                            creditCardCo,
                            -3_900,
                            "Credit card payment",
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
                        Categorized(
                            checkingBank,
                            checking,
                            salary,
                            employer,
                            260_000,
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

                // Quarterly portfolio revaluation against Unrealized Gains — mostly growth with
                // one losing quarter so both directions appear. Hand entries, no bank import.
                if (monthStart.Month % 3 == 0)
                    OnDay(
                        monthStart,
                        30,
                        d =>
                            Cash(
                                investments,
                                unrealizedGains,
                                null,
                                PortfolioRevaluation(monthStart.Month),
                                "Portfolio revaluation",
                                d
                            )
                    );

                // Annual WOZ-style home revaluation — once a year, like the municipal assessment.
                if (monthStart.Month == 2)
                    OnDay(
                        monthStart,
                        20,
                        d => Cash(home, unrealizedGains, null, 1_200_000, "WOZ revaluation", d)
                    );

                // Construction deposit draws (ADR-0026): the lender pays a contractor directly as
                // the build progresses, so the deposit (an Illiquid Asset) shrinks and the Home
                // value rises — a balanced hand entry, no bank import. Three €9,000 draws taper the
                // deposit from €40,000 to €13,000, so the deposit-interest offset shrinks with it.
                if (m is 9 or 6 or 3)
                {
                    OnDay(
                        monthStart,
                        9,
                        d =>
                            Cash(
                                home,
                                constructionDeposit,
                                null,
                                900_000,
                                "Construction deposit draw",
                                d
                            )
                    );
                    depositBalance -= 900_000;
                }
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

            // Outlook templates (ADR-0027): user-confirmed recurring patterns the forward-looking
            // Projection runs on. Pinned to the Liquid checking/savings accounts and keyed on the
            // same counterparty / counter-account as the postings above, so Occurrence matching
            // recognizes the seeded actuals and excludes them from the Typical-spend band (rather
            // than double-counting). ExpectedAmount is raw ledger-signed on the pinned leg: outflow
            // negative, inflow positive. Anchors sit on a real past occurrence so cadence stepping
            // lines up with the ledger. Groceries, dining and ad-hoc spend stay untemplated — they
            // are exactly the everyday residual the Typical-spend band is meant to capture.
            Template("Salary", checking, salary, employer, Cadence.Monthly, 25, 260_000);
            Template(
                "Internet & phone",
                checking,
                internetAndPhone,
                telecom,
                Cadence.Monthly,
                3,
                -4_500
            );
            Template("Energy", checking, energy, utilityCo, Cadence.Monthly, 4, -8_000);
            Template(
                "Health insurance",
                checking,
                healthInsurance,
                healthInsurer,
                Cadence.Monthly,
                5,
                -13_500
            );
            Template("Gym membership", checking, sports, gym, Cadence.Monthly, 8, -2_500);
            Template(
                "Subscriptions",
                checking,
                subscriptions,
                streaming,
                Cadence.Monthly,
                13,
                -2_580
            );
            Template(
                "Investment contribution",
                checking,
                investments,
                broker,
                Cadence.Monthly,
                26,
                -25_000
            );

            // The standing checking → savings transfer: a recurring commitment with no P&L leg, so
            // it is keyed on its counter liquid account. Mirrored on each side of the move.
            Template("Transfer to savings", checking, savings, null, Cadence.Monthly, 28, -100_000);
            Template(
                "Transfer from checking",
                savings,
                checking,
                null,
                Cadence.Monthly,
                28,
                100_000
            );

            // Quarterly and annual commitments — these shape the year-end figure (the big summer
            // holiday outflow, the spring tax refund inflow, the annual insurance and taxes).
            Template(
                "Water",
                checking,
                water,
                waterCo,
                Cadence.Quarterly,
                m => m % 3 == 1,
                20,
                -3_000
            );
            Template(
                "Car service",
                checking,
                car,
                garage,
                Cadence.Quarterly,
                m => m % 3 == 2,
                16,
                -9_000
            );
            Template(
                "Municipal taxes",
                checking,
                taxes,
                taxOffice,
                Cadence.Yearly,
                m => m == 3,
                9,
                -45_000
            );
            Template(
                "Home insurance",
                checking,
                housingInsurance,
                housingInsurer,
                Cadence.Yearly,
                m => m == 1,
                15,
                -28_000
            );
            Template(
                "Summer holiday",
                checking,
                holidays,
                travelCo,
                Cadence.Yearly,
                m => m == 7,
                5,
                -180_000
            );
            Template(
                "Tax refund",
                checking,
                taxReturn,
                taxOffice,
                Cadence.Yearly,
                m => m == 5,
                13,
                60_000
            );

            // A planned one-off (Cadence.Once): a purchase scheduled for next month, with no
            // matching posting yet — it shows purely as an upcoming expected item in the Outlook.
            Template(
                "New laptop",
                checking,
                shopping,
                onlineStore,
                Cadence.Once,
                FirstOfMonth(_today).AddMonths(1).AddDays(11),
                -150_000
            );

            return new DevelopmentSeedGraph
            {
                Accounts = _accounts,
                Counterparties = _counterparties,
                BankAccounts = _bankAccounts,
                JournalEntries = _journalEntries,
                BankTransactions = _bankTransactions,
                Loans = _loans,
                LoanParts = _loanParts,
                LoanPartRatePeriods = _loanPartRatePeriods,
                JournalEntryTemplates = _journalEntryTemplates,
            };
        }

        // A categorized bank row: a balanced entry whose bank-side line is Cleared (or Reconciled),
        // plus the imported BankTransaction attached to it. The bank-tx amount equals the bank-side
        // line amount under the deliberate raw-equality match (ADR-0012).
        private void Categorized(
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

        // An opening balance: a single hand-entered entry pairing the account against the seeded
        // Opening Balances equity account (AccountSeed). Both legs Reconciled; no bank import.
        private void Opening(Account account, long amount, DateOnly date) =>
            AddEntry(
                date,
                "Opening balance",
                counterpartyId: null,
                new LineSpec(account.Id, amount, ReconciliationStatus.Reconciled),
                new LineSpec(
                    AccountSeed.OpeningBalancesId,
                    -amount,
                    ReconciliationStatus.Reconciled
                )
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

        // A loan payment in its loan-aware posted shape (ADR-0025): the single bank debit
        // covering every part, with each counter-line attributed to its Loan Part. When a
        // construction-deposit offset is present (ADR-0026), a loan-level Income credit (no Loan
        // Part attribution) nets the gross interest down to the single debit the bank collects.
        // The bank-tx carries the lender's IBAN so re-categorizing it later resolves the hint again.
        private void LoanPayment(
            BankAccount bank,
            Account bankSide,
            Counterparty lender,
            string lenderIban,
            DateOnly date,
            Account depositInterest,
            long depositOffset,
            params LoanLineSpec[] parts
        )
        {
            var gross = parts.Sum(p => p.Amount);
            var net = gross - depositOffset;
            var specs = new List<LineSpec>(parts.Length + 2)
            {
                new(bankSide.Id, -net, ReconciliationStatus.Cleared),
            };
            foreach (var part in parts)
                specs.Add(
                    new LineSpec(
                        part.Account,
                        part.Amount,
                        ReconciliationStatus.Uncleared,
                        part.Part.Id
                    )
                );
            if (depositOffset > 0)
                specs.Add(
                    new LineSpec(depositInterest.Id, -depositOffset, ReconciliationStatus.Uncleared)
                );

            var entry = AddEntry(date, "Mortgage payment", lender.Id, [.. specs]);
            AddBankTransaction(
                bank.Id,
                date,
                -net,
                "Mortgage payment",
                lender.Name,
                entry.Id,
                counterpartyAccountNumber: lenderIban
            );
        }

        // The deposit-interest offset for a month (ADR-0026): the deposit balance at month start
        // times the monthly rate, capped at the entry's gross interest so the payment never flips
        // sign. Mirrors LoanProjectionService so the seeded actuals match the live proposal.
        private static long DepositOffset(
            long depositBalance,
            decimal annualRatePercent,
            long grossInterest
        )
        {
            if (depositBalance <= 0 || annualRatePercent <= 0m)
                return 0;
            var monthly = (long)
                Math.Round(
                    depositBalance * annualRatePercent / 100m / 12m,
                    0,
                    MidpointRounding.AwayFromZero
                );
            return Math.Min(Math.Max(0L, monthly), grossInterest);
        }

        // An Outlook recurring-item template (ADR-0027) pinned to a Liquid account, keyed on its
        // counter account and (when known) its counterparty. ExpectedAmount is raw ledger-signed on
        // the pinned leg. No SEPA ids are seeded, so matching falls to the counterparty / counter
        // account — exactly the signals the seeded postings carry.
        private JournalEntryTemplate Template(
            string name,
            Account account,
            Account counter,
            Counterparty? counterparty,
            Cadence cadence,
            DateOnly anchorDate,
            long expectedAmount
        )
        {
            var template = new JournalEntryTemplate
            {
                Id = new JournalEntryTemplateId(Guid.CreateVersion7()),
                Name = name,
                AccountId = account.Id,
                CounterAccountId = counter.Id,
                CounterpartyId = counterparty?.Id,
                Cadence = cadence,
                AnchorDate = anchorDate,
                ExpectedAmount = expectedAmount,
                CreatedAt = _structuralCreatedAt,
                UpdatedAt = _structuralCreatedAt,
            };
            _journalEntryTemplates.Add(template);
            return template;
        }

        // Monthly template anchored to the given day-of-month in the oldest seeded month, so the
        // anchor coincides with the first posted occurrence.
        private JournalEntryTemplate Template(
            string name,
            Account account,
            Account counter,
            Counterparty? counterparty,
            Cadence cadence,
            int day,
            long expectedAmount
        ) =>
            Template(
                name,
                account,
                counter,
                counterparty,
                cadence,
                FirstOfMonth(_today).AddMonths(-11).AddDays(day - 1),
                expectedAmount
            );

        // Quarterly / yearly template anchored to the first month in the trailing window whose
        // calendar month matches the predicate, so cadence stepping lands on the same months as the
        // seeded periodic postings (and matches them for Typical-spend exclusion).
        private JournalEntryTemplate Template(
            string name,
            Account account,
            Account counter,
            Counterparty? counterparty,
            Cadence cadence,
            Func<int, bool> when,
            int day,
            long expectedAmount
        ) =>
            Template(
                name,
                account,
                counter,
                counterparty,
                cadence,
                FirstMatchingMonth(when, day),
                expectedAmount
            );

        private DateOnly FirstMatchingMonth(Func<int, bool> when, int day)
        {
            for (var m = 11; m >= 0; m--)
            {
                var monthStart = FirstOfMonth(_today).AddMonths(-m);
                if (when(monthStart.Month))
                    return monthStart.AddDays(day - 1);
            }

            return FirstOfMonth(_today).AddMonths(-11).AddDays(day - 1);
        }

        private void Inbox(
            BankAccount bank,
            DateOnly date,
            long amount,
            string description,
            string counterpartyName,
            string? counterpartyAccountNumber = null
        )
        {
            if (date <= _today)
                AddBankTransaction(
                    bank.Id,
                    date,
                    amount,
                    description,
                    counterpartyName,
                    journalEntryId: null,
                    counterpartyAccountNumber: counterpartyAccountNumber
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
            params LineSpec[] lines
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
            foreach (var spec in lines)
                entry.Lines.Add(AddLine(id, spec, at));
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
                LoanPartId = spec.LoanPart,
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
            (DateTime At, string Reason)? dismissed = null,
            string? counterpartyAccountNumber = null
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
                    CounterpartyAccountNumber = counterpartyAccountNumber,
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

        // Quarterly mark-to-market deltas for the Investments account, keyed off the calendar
        // month: growth in three quarters, one losing quarter (June), so reports show both signs.
        private static long PortfolioRevaluation(int month) =>
            month switch
            {
                3 => 40_000,
                6 => -25_000,
                9 => 55_000,
                12 => 35_000,
                _ => 0,
            };

        private Account Branch(
            string code,
            string name,
            AccountType type,
            Account? parent = null
        ) => AddAccount(code, name, type, postable: false, liquid: true, parent: parent?.Id);

        private Account Leaf(
            string code,
            string name,
            AccountType type,
            Account? parent = null,
            bool liquid = true
        ) => AddAccount(code, name, type, postable: true, liquid, parent?.Id);

        private Account AddAccount(
            string code,
            string name,
            AccountType type,
            bool postable,
            bool liquid,
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
                IsLiquid = liquid,
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

        private Loan Loan(string name, Counterparty lender, Account interestExpense, Account parent)
        {
            var loan = new Loan
            {
                Id = new LoanId(Guid.CreateVersion7()),
                Name = name,
                LenderCounterpartyId = lender.Id,
                InterestExpenseAccountId = interestExpense.Id,
                ParentAccountId = parent.Id,
                CreatedAt = _structuralCreatedAt,
                UpdatedAt = _structuralCreatedAt,
            };
            _loans.Add(loan);
            return loan;
        }

        private LoanPart LoanPart(
            Loan loan,
            string label,
            LoanRepaymentType repaymentType,
            DateOnly startDate,
            DateOnly endDate,
            Account account
        )
        {
            var part = new LoanPart
            {
                Id = new LoanPartId(Guid.CreateVersion7()),
                LoanId = loan.Id,
                Label = label,
                RepaymentType = repaymentType,
                StartDate = startDate,
                EndDate = endDate,
                AccountId = account.Id,
                CreatedAt = _structuralCreatedAt,
                UpdatedAt = _structuralCreatedAt,
            };
            _loanParts.Add(part);
            return part;
        }

        private void RatePeriod(
            LoanPart part,
            DateOnly effectiveDate,
            decimal annualRatePercent,
            DateOnly? fixedUntil
        ) =>
            _loanPartRatePeriods.Add(
                new LoanPartRatePeriod
                {
                    Id = new LoanPartRatePeriodId(Guid.CreateVersion7()),
                    LoanPartId = part.Id,
                    EffectiveDate = effectiveDate,
                    AnnualRatePercent = annualRatePercent,
                    FixedUntil = fixedUntil,
                    CreatedAt = _structuralCreatedAt,
                    UpdatedAt = _structuralCreatedAt,
                }
            );

        // A counterparty-owned bank account (ADR-0010): the other side's IBAN, no own Account.
        private void CounterpartyBankAccount(Counterparty counterparty, string iban) =>
            _bankAccounts.Add(
                new BankAccount
                {
                    Id = new BankAccountId(Guid.CreateVersion7()),
                    Type = BankAccountType.Current,
                    Iban = iban,
                    CounterpartyId = counterparty.Id,
                    CreatedAt = _structuralCreatedAt,
                    UpdatedAt = _structuralCreatedAt,
                }
            );

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
