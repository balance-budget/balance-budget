import { useMemo, useState } from 'react';
import { Trans, useLingui } from '@lingui/react/macro';
import {
    useDeleteTemplate,
    useOutlookProjection,
    useOutlookTemplates,
    useTemplateCandidates,
    type JournalEntryTemplate,
    type OutlookAccountProjection,
    type OutlookExpectedItem,
    type TemplateCandidate,
    type WireScenarioRequest,
} from '../api/outlook';
import { useAccounts } from '../api/accounts';
import { useCurrencyCatalog } from '../api/currencies';
import { Amount } from '../components/Amount';
import { Empty } from '../components/Empty';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { Button } from '../components/ui/Button';
import { Checkbox } from '../components/ui/Checkbox';
import { ToggleButton, ToggleButtonGroup } from '../components/ui/ToggleButtonGroup';
import { useToast } from '../components/ui/Toast';
import { accountPathLabel, accountPathSegments, ACCOUNT_PATH_SEPARATOR } from '../lib/accountTree';
import { cx } from '../lib/cx';
import { type AccountId, type JournalEntryTemplateId } from '../lib/domain';
import { formatCalendarDate } from '../i18n/format';
import { formatMoney } from '../lib/money';
import { JournalEntryTemplateForm } from './JournalEntryTemplateForm';
import { OutlookProjectionChart } from './OutlookProjectionChart';

const CURRENCY = 'EUR';
const HORIZONS = [6, 12, 24] as const;

function formatDueDate(iso: string): string {
    return formatCalendarDate(iso, 'year-month-day');
}

function formatMonthName(iso: string): string {
    return formatCalendarDate(iso, 'year-month', { style: 'long' });
}

function formatDayMonth(iso: string): string {
    return formatCalendarDate(iso, 'month-day');
}

export function Outlook() {
    const [horizon, setHorizon] = useState<number>(12);
    const [selectedId, setSelectedId] = useState<AccountId | null>(null);
    const [disabledIds, setDisabledIds] = useState<Set<JournalEntryTemplateId>>(new Set());
    const [creating, setCreating] = useState(false);
    const [editing, setEditing] = useState<JournalEntryTemplate | null>(null);
    const [accepting, setAccepting] = useState<TemplateCandidate | null>(null);

    const scenario = useMemo<WireScenarioRequest | null>(
        () =>
            disabledIds.size === 0
                ? null
                : {
                      disabledTemplateIds: [...disabledIds],
                      addedTemplates: [],
                      amountOverrides: [],
                  },
        [disabledIds],
    );

    const projection = useOutlookProjection(CURRENCY, horizon, scenario);

    // The selected account drives the whole page — chart, cards, and the lists below.
    const accounts = projection.data?.accounts ?? [];
    const selectedAccount = accounts.find(a => a.accountId === selectedId) ?? accounts[0] ?? null;

    const toggleDisabled = (id: JournalEntryTemplateId) => {
        setDisabledIds(prev => {
            const next = new Set(prev);
            if (next.has(id)) next.delete(id);
            else next.add(id);
            return next;
        });
    };

    return (
        <>
            <ProjectionPanel
                projection={projection}
                selected={selectedAccount}
                onSelect={setSelectedId}
                horizon={horizon}
                onHorizon={setHorizon}
                hasScenario={scenario !== null}
                onResetScenario={() => {
                    setDisabledIds(new Set());
                }}
            />

            {/* Recurring items over detected patterns, stacked full-width — an
                empty/short panel beside a long list left a large empty quadrant. */}
            <section className="grid gap-[18px] grid-cols-1 mt-[18px]">
                <RecurringPanel
                    accountId={selectedAccount?.accountId ?? null}
                    accountName={selectedAccount?.accountName ?? null}
                    disabledIds={disabledIds}
                    onToggleDisabled={toggleDisabled}
                    onAdd={() => {
                        setCreating(true);
                    }}
                    onEdit={setEditing}
                />
                <CandidatesPanel
                    accountId={selectedAccount?.accountId ?? null}
                    accountName={selectedAccount?.accountName ?? null}
                    onAccept={setAccepting}
                />
            </section>

            {creating && (
                <JournalEntryTemplateForm
                    defaultAccountId={selectedAccount?.accountId}
                    onClose={() => {
                        setCreating(false);
                    }}
                />
            )}
            {editing && (
                <JournalEntryTemplateForm
                    template={editing}
                    onClose={() => {
                        setEditing(null);
                    }}
                />
            )}
            {accepting && (
                <JournalEntryTemplateForm
                    candidate={accepting}
                    onClose={() => {
                        setAccepting(null);
                    }}
                />
            )}
        </>
    );
}

function ProjectionPanel({
    projection,
    selected,
    onSelect,
    horizon,
    onHorizon,
    hasScenario,
    onResetScenario,
}: {
    projection: ReturnType<typeof useOutlookProjection>;
    selected: OutlookAccountProjection | null;
    onSelect: (id: AccountId) => void;
    horizon: number;
    onHorizon: (h: number) => void;
    hasScenario: boolean;
    onResetScenario: () => void;
}) {
    const { t } = useLingui();

    const allAccounts = useAccounts();
    const byId = useMemo(
        () => new Map((allAccounts.data ?? []).map(a => [a.id, a])),
        [allAccounts.data],
    );
    const pathLabel = (id: AccountId, fallback: string) => {
        const segments = accountPathSegments(byId, id);
        return segments.length > 0 ? segments.join(ACCOUNT_PATH_SEPARATOR) : fallback;
    };

    const accounts = projection.data?.accounts ?? [];

    return (
        <Panel>
            <SectionHead
                subtitle={
                    <Trans>Where your balances are heading, from your recurring items.</Trans>
                }
                action={
                    <div className="flex items-center gap-2">
                        {hasScenario && (
                            <Button variant="ghost" onPress={onResetScenario}>
                                <Trans>Reset what-if</Trans>
                            </Button>
                        )}
                        <ToggleButtonGroup
                            aria-label={t`Horizon`}
                            disallowEmptySelection
                            selectedKeys={[String(horizon)]}
                            onSelectionChange={keys => {
                                const next = [...keys][0];
                                if (next !== undefined) onHorizon(Number(next));
                            }}
                        >
                            {HORIZONS.map(h => (
                                <ToggleButton key={h} id={String(h)}>
                                    {t`${h}m`}
                                </ToggleButton>
                            ))}
                        </ToggleButtonGroup>
                    </div>
                }
            />

            {projection.isPending ? (
                <Skeleton className="h-[300px]" />
            ) : projection.isError ? (
                <ErrorState
                    message={t`Couldn't load the projection.`}
                    onRetry={() => void projection.refetch()}
                />
            ) : accounts.length === 0 ? (
                <Empty
                    title={t`Nothing to project yet`}
                    hint={t`Add a liquid account and a few recurring items to see where your balance is heading.`}
                />
            ) : (
                <div className="flex flex-col gap-4">
                    <div className="flex flex-wrap gap-2">
                        {accounts.map(a => (
                            <button
                                key={a.accountId}
                                type="button"
                                onClick={() => {
                                    onSelect(a.accountId);
                                }}
                                className={cx(
                                    'px-3 py-1.5 rounded-lg text-sm font-medium transition-colors',
                                    a.accountId === selected?.accountId
                                        ? 'bg-brand-primary-soft text-brand-primary'
                                        : 'text-fg-2 hover:bg-surface-2',
                                )}
                            >
                                {pathLabel(a.accountId, a.accountName)}
                            </button>
                        ))}
                    </div>

                    {selected && <ProjectionDetail account={selected} />}
                </div>
            )}
        </Panel>
    );
}

function ProjectionDetail({ account }: { account: OutlookAccountProjection }) {
    return (
        <>
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-3">
                <ThisMonthCard account={account} />
                <YearEndCard account={account} />
            </div>

            <OutlookProjectionChart account={account} />
        </>
    );
}

function ThisMonthCard({ account }: { account: OutlookAccountProjection }) {
    const m = account.thisMonth;
    return (
        <div className="flex flex-col gap-2 p-4 rounded-xl bg-surface-2">
            <div className="flex items-baseline justify-between">
                <span className="text-xs font-medium text-fg-3 uppercase tracking-wide">
                    <Trans>This month</Trans>
                </span>
                <span className="text-xs text-fg-3">{formatMonthName(m.month)}</span>
            </div>

            <ExpectedGroup
                label={<Trans>Still expected in</Trans>}
                total={m.expectedIn}
                items={m.items.filter(i => i.amount > 0)}
                currencyCode={account.currencyCode}
            />
            <ExpectedGroup
                label={<Trans>Still expected out</Trans>}
                total={m.expectedOut}
                items={m.items.filter(i => i.amount < 0)}
                currencyCode={account.currencyCode}
            />
            <CardRow label={<Trans>Everyday spending</Trans>}>
                <RangeAmount
                    low={m.everydaySpendLow}
                    high={m.everydaySpendHigh}
                    currencyCode={account.currencyCode}
                />
            </CardRow>

            <div className="border-t border-border-soft my-1" />

            <CardRow label={<Trans>Balance now</Trans>}>
                <Amount
                    minor={account.currentBalance}
                    currencyCode={account.currencyCode}
                    size="inline"
                />
            </CardRow>
            <CardRow label={<Trans>End of month</Trans>} emphasis>
                <EstimatedRange
                    low={m.endBalanceLow}
                    high={m.endBalanceHigh}
                    currencyCode={account.currencyCode}
                />
            </CardRow>
        </div>
    );
}

function YearEndCard({ account }: { account: OutlookAccountProjection }) {
    const y = account.yearEnd;
    const scenarioEnd =
        account.scenario?.find(month => month.month.slice(0, 7) === y.date.slice(0, 7))
            ?.endBalanceMid ?? null;
    const year = y.date.slice(0, 4);

    return (
        <div className="flex flex-col gap-2 p-4 rounded-xl bg-surface-2">
            <div className="flex items-baseline justify-between">
                <span className="text-xs font-medium text-fg-3 uppercase tracking-wide">
                    <Trans>Year-end</Trans>
                </span>
                <span className="text-xs text-fg-3">
                    <Trans>31 Dec {year}</Trans>
                </span>
            </div>

            <EstimatedRange
                low={y.endBalanceLow}
                high={y.endBalanceHigh}
                currencyCode={account.currencyCode}
                align="start"
            />

            {scenarioEnd !== null && (
                <div className="flex items-baseline justify-between mt-auto pt-2">
                    <span className="text-xs text-fg-3">
                        <Trans>What-if</Trans>
                    </span>
                    <span className="text-warning">
                        <Amount
                            minor={scenarioEnd}
                            currencyCode={account.currencyCode}
                            size="inline"
                        />
                    </span>
                </div>
            )}
        </div>
    );
}

function CardRow({
    label,
    emphasis,
    children,
}: {
    label: React.ReactNode;
    emphasis?: boolean;
    children: React.ReactNode;
}) {
    return (
        <div className="flex items-baseline justify-between gap-x-4">
            <span className={cx('text-sm', emphasis ? 'text-fg-1 font-medium' : 'text-fg-2')}>
                {label}
            </span>
            <span className="flex flex-col items-end">{children}</span>
        </div>
    );
}

function RangeAmount({
    low,
    high,
    currencyCode,
    size = 'inline',
}: {
    low: number;
    high: number;
    currencyCode: string;
    size?: 'inline' | 'medium';
}) {
    return (
        <span className="inline-flex items-baseline gap-1 tabular-nums">
            <Amount minor={low} currencyCode={currencyCode} size={size} decimals={false} />
            <span className="text-fg-3">–</span>
            <Amount minor={high} currencyCode={currencyCode} size={size} decimals={false} />
        </span>
    );
}

/**
 * A signed money figure colored by sign the same way the Activity feed does (green in, red out) —
 * a flat formatted string, not the tri-color {@link Amount}, so the whole number takes the color.
 */
function SignedAmount({
    minor,
    currencyCode,
    small,
}: {
    minor: number;
    currencyCode: string;
    small?: boolean;
}) {
    const catalog = useCurrencyCatalog();
    // Zero is neither in nor out — keep it neutral rather than reading as a gain.
    const color = minor === 0 ? 'text-fg-3' : minor < 0 ? 'text-danger' : 'text-success';
    return (
        <span className={cx('font-mono tabular-nums', small ? 'text-xs' : 'text-sm', color)}>
            {formatMoney(minor, currencyCode, catalog, { sign: true })}
        </span>
    );
}

/** A still-expected direction (in or out): the colored total, with its per-item breakdown beneath. */
function ExpectedGroup({
    label,
    total,
    items,
    currencyCode,
}: {
    label: React.ReactNode;
    total: number;
    items: OutlookExpectedItem[];
    currencyCode: string;
}) {
    return (
        <div className="flex flex-col gap-1">
            <CardRow label={label} emphasis>
                <SignedAmount minor={total} currencyCode={currencyCode} />
            </CardRow>
            {items.length > 0 && (
                <div className="flex flex-col gap-0.5 pl-3">
                    {items.map((item, i) => (
                        <div
                            key={i}
                            className="flex items-baseline justify-between gap-x-4 text-xs"
                        >
                            <span className="text-fg-3 truncate">
                                {item.name}
                                <span className="text-fg-3"> · {formatDayMonth(item.dueDate)}</span>
                            </span>
                            <SignedAmount minor={item.amount} currencyCode={currencyCode} small />
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

/** A low–high range with an explicit "Estimated" caption, so it never reads as a known figure. */
function EstimatedRange({
    low,
    high,
    currencyCode,
    align = 'end',
}: {
    low: number;
    high: number;
    currencyCode: string;
    align?: 'start' | 'end';
}) {
    return (
        <span
            className={cx('flex flex-col gap-0.5', align === 'end' ? 'items-end' : 'items-start')}
        >
            <RangeAmount low={low} high={high} currencyCode={currencyCode} size="medium" />
            <span className="text-[10px] font-medium text-fg-3 uppercase tracking-wide">
                <Trans>Estimated</Trans>
            </span>
        </span>
    );
}

function RecurringPanel({
    accountId,
    accountName,
    disabledIds,
    onToggleDisabled,
    onAdd,
    onEdit,
}: {
    accountId: AccountId | null;
    accountName: string | null;
    disabledIds: Set<JournalEntryTemplateId>;
    onToggleDisabled: (id: JournalEntryTemplateId) => void;
    onAdd: () => void;
    onEdit: (template: JournalEntryTemplate) => void;
}) {
    const { t } = useLingui();
    const templates = useOutlookTemplates();
    const remove = useDeleteTemplate();
    const toast = useToast();
    const allAccounts = useAccounts();
    const byId = useMemo(
        () => new Map((allAccounts.data ?? []).map(a => [a.id, a])),
        [allAccounts.data],
    );

    const visible = (templates.data ?? []).filter(tpl => tpl.accountId === accountId);

    async function onDelete(template: JournalEntryTemplate) {
        try {
            await remove.mutateAsync(template.id);
            toast.success(t`Recurring item removed.`);
        } catch {
            toast.error(t`Couldn't remove the recurring item.`);
        }
    }

    return (
        <Panel>
            <SectionHead
                title={<Trans>Recurring items</Trans>}
                subtitle={
                    accountName ? (
                        <Trans>Expected payments and income for {accountName}.</Trans>
                    ) : (
                        <Trans>Expected payments and income that shape the projection.</Trans>
                    )
                }
                action={
                    <Button variant="secondary" onPress={onAdd}>
                        <Icon name="plus" size={14} />
                        <Trans>New</Trans>
                    </Button>
                }
            />

            {templates.isPending ? (
                <Skeleton className="h-24" />
            ) : templates.isError ? (
                <ErrorState
                    message={t`Couldn't load recurring items.`}
                    onRetry={() => void templates.refetch()}
                />
            ) : visible.length === 0 ? (
                <Empty
                    title={t`No recurring items yet`}
                    hint={t`Add your rent, salary, and subscriptions, or accept a detected one.`}
                />
            ) : (
                <div className="flex flex-col divide-y divide-border-soft">
                    {visible.map(template => (
                        <TemplateRow
                            key={template.id}
                            template={template}
                            accountLabel={
                                accountPathLabel(byId, template.accountId) ?? template.accountName
                            }
                            disabled={disabledIds.has(template.id)}
                            onToggleDisabled={() => {
                                onToggleDisabled(template.id);
                            }}
                            onEdit={() => {
                                onEdit(template);
                            }}
                            onDelete={() => void onDelete(template)}
                        />
                    ))}
                </div>
            )}
        </Panel>
    );
}

function TemplateRow({
    template,
    accountLabel,
    disabled,
    onToggleDisabled,
    onEdit,
    onDelete,
}: {
    template: JournalEntryTemplate;
    accountLabel: string;
    disabled: boolean;
    onToggleDisabled: () => void;
    onEdit: () => void;
    onDelete: () => void;
}) {
    const { t } = useLingui();
    return (
        <div className={cx('flex items-center gap-3 py-3', disabled && 'opacity-50')}>
            <Checkbox
                isSelected={!disabled}
                onChange={onToggleDisabled}
                aria-label={t`Include in what-if`}
            />
            <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-fg-1 truncate">{template.name}</span>
                    <span className="text-xs text-fg-3 shrink-0">{template.cadence}</span>
                </div>
                <div className="text-xs text-fg-3 truncate">
                    {accountLabel}
                    {template.nextDueDate && (
                        <>
                            {' · '}
                            <Trans>next {formatDueDate(template.nextDueDate)}</Trans>
                        </>
                    )}
                </div>
            </div>
            <div className="text-right shrink-0">
                <Amount
                    minor={template.monthlyEquivalent}
                    currencyCode={template.currencyCode}
                    size="inline"
                    sign
                />
                <div className="text-[11px] text-fg-3">
                    <Trans>/mo</Trans>
                </div>
            </div>
            <div className="flex items-center gap-1 shrink-0">
                <Button variant="ghost" onPress={onEdit} aria-label={t`Edit`}>
                    <Icon name="pencil" size={14} />
                </Button>
                <Button variant="ghost" onPress={onDelete} aria-label={t`Delete`}>
                    <Icon name="trash" size={14} />
                </Button>
            </div>
        </div>
    );
}

function CandidatesPanel({
    accountId,
    accountName,
    onAccept,
}: {
    accountId: AccountId | null;
    accountName: string | null;
    onAccept: (candidate: TemplateCandidate) => void;
}) {
    const { t } = useLingui();
    const candidates = useTemplateCandidates();
    const allAccounts = useAccounts();
    const byId = useMemo(
        () => new Map((allAccounts.data ?? []).map(a => [a.id, a])),
        [allAccounts.data],
    );

    const visible = (candidates.data ?? []).filter(c => c.accountId === accountId);

    return (
        <Panel>
            <SectionHead
                title={<Trans>Detected</Trans>}
                subtitle={
                    accountName ? (
                        <Trans>Patterns we spotted for {accountName}.</Trans>
                    ) : (
                        <Trans>Patterns we spotted in your history.</Trans>
                    )
                }
            />

            {candidates.isPending ? (
                <Skeleton className="h-24" />
            ) : candidates.isError ? (
                <ErrorState
                    message={t`Couldn't load suggestions.`}
                    onRetry={() => void candidates.refetch()}
                />
            ) : visible.length === 0 ? (
                <Empty
                    title={t`Nothing detected`}
                    hint={t`We'll suggest recurring items as your history grows.`}
                />
            ) : (
                <div className="flex flex-col divide-y divide-border-soft">
                    {visible.map((candidate, i) => (
                        <div key={i} className="flex items-center gap-3 py-3">
                            <div className="flex-1 min-w-0">
                                <div className="text-sm font-medium text-fg-1 truncate">
                                    {candidate.suggestedName}
                                </div>
                                <div className="text-xs text-fg-3 truncate">
                                    {accountPathLabel(byId, candidate.accountId) ??
                                        candidate.accountName}{' '}
                                    · {candidate.cadence} ·{' '}
                                    <Trans>{candidate.occurrenceCount} seen</Trans>
                                </div>
                            </div>
                            <Amount
                                minor={candidate.monthlyEquivalent}
                                currencyCode={candidate.currencyCode}
                                size="inline"
                                sign
                            />
                            <Button
                                variant="secondary"
                                onPress={() => {
                                    onAccept(candidate);
                                }}
                            >
                                <Trans>Add</Trans>
                            </Button>
                        </div>
                    ))}
                </div>
            )}
        </Panel>
    );
}
