import { Fragment } from 'react';
import { useLingui } from '@lingui/react/macro';
import type { CurrencyCatalog } from '../api/currencies';
import type { BankTransactionDetail } from '../api/bankTransactions';
import { cx } from '../lib/cx';
import { formatMoney } from '../lib/money';

type Field = { label: string; value: string; tone?: 'positive' | 'negative' };

export function BankTransactionDetails({
    bt,
    catalog,
}: {
    bt: BankTransactionDetail;
    catalog: CurrencyCatalog;
}) {
    const { t } = useLingui();
    const fields: Field[] = [];

    fields.push({ label: t`Date`, value: bt.bookingDate });
    fields.push({ label: t`Description`, value: bt.description });
    fields.push({
        label: t`Counterparty`,
        value: formatCounterparty(bt.counterpartyName, bt.counterpartyAccountNumber),
    });
    fields.push({
        label: t`Amount`,
        value: formatMoney(bt.money.amount, bt.money.currencyCode, catalog, { sign: true }),
        tone: bt.money.amount < 0 ? 'negative' : 'positive',
    });
    if (bt.valueDate !== null) fields.push({ label: t`Value date`, value: bt.valueDate });
    if (bt.reference !== null) fields.push({ label: t`Reference`, value: bt.reference });
    if (bt.mandateId !== null) fields.push({ label: t`Mandate ID`, value: bt.mandateId });
    if (bt.sepaCreditorId !== null) {
        fields.push({ label: t`SEPA creditor ID`, value: bt.sepaCreditorId });
    }
    if (bt.foreignAmount !== null && bt.foreignCurrencyCode !== null) {
        fields.push({
            label: t`Foreign amount`,
            value: formatMoney(bt.foreignAmount, bt.foreignCurrencyCode, catalog, {
                sign: false,
            }),
        });
    }
    if (bt.exchangeRate !== null) {
        fields.push({ label: t`Exchange rate`, value: bt.exchangeRate.toString() });
    }
    if (bt.importerKey !== null) fields.push({ label: t`Importer`, value: bt.importerKey });

    for (const entry of bt.metadata) {
        const value =
            entry.integerValue !== null ? entry.integerValue.toString() : (entry.stringValue ?? '');
        fields.push({ label: entry.key, value });
    }

    return (
        <dl className="px-3 py-2 rounded-lg bg-surface-2 border border-border-soft grid grid-cols-[max-content_1fr] gap-x-3 gap-y-1 text-xs">
            {fields.map(field => (
                <Fragment key={field.label}>
                    <dt className="text-fg-3">{field.label}</dt>
                    <dd
                        className={cx(
                            'tabular-nums truncate',
                            field.tone === 'negative'
                                ? 'text-danger font-mono'
                                : field.tone === 'positive'
                                  ? 'text-success font-mono'
                                  : 'text-fg-1',
                        )}
                    >
                        {field.value}
                    </dd>
                </Fragment>
            ))}
        </dl>
    );
}

function formatCounterparty(name: string | null, iban: string | null): string {
    if (name && iban) return `${name} · ${iban}`;
    if (name) return name;
    if (iban) return iban;
    return '—';
}
