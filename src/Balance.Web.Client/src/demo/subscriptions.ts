import { asSubscriptionId, type SubscriptionSummary } from '../lib/domain';

export const SUBSCRIPTIONS: SubscriptionSummary[] = [
    {
        id: asSubscriptionId('019709a8-4f60-7c11-b6a1-3c2c1a3da101'),
        counterpartyName: 'Netflix',
        amountMinor: 13_99,
        currencyCode: 'EUR',
        cadence: 'monthly',
        nextDate: '2026-05-22',
        iconName: 'tv',
    },
    {
        id: asSubscriptionId('019709a8-4f60-7c11-b6a1-3c2c1a3da102'),
        counterpartyName: 'Spotify',
        amountMinor: 11_99,
        currencyCode: 'EUR',
        cadence: 'monthly',
        nextDate: '2026-05-24',
        iconName: 'music',
    },
    {
        id: asSubscriptionId('019709a8-4f60-7c11-b6a1-3c2c1a3da103'),
        counterpartyName: 'Adobe Creative Cloud',
        amountMinor: 59_99,
        currencyCode: 'EUR',
        cadence: 'monthly',
        nextDate: '2026-05-28',
        iconName: 'palette',
    },
    {
        id: asSubscriptionId('019709a8-4f60-7c11-b6a1-3c2c1a3da104'),
        counterpartyName: 'iCloud+',
        amountMinor: 2_99,
        currencyCode: 'EUR',
        cadence: 'monthly',
        nextDate: '2026-06-01',
        iconName: 'cloud',
    },
    {
        id: asSubscriptionId('019709a8-4f60-7c11-b6a1-3c2c1a3da105'),
        counterpartyName: 'NS Flex',
        amountMinor: 17_50,
        currencyCode: 'EUR',
        cadence: 'monthly',
        nextDate: '2026-06-03',
        iconName: 'train',
    },
];
