import { useMemo, useState } from 'react';
import { Trans, useLingui } from '@lingui/react/macro';
import {
    useDeleteTemplate,
    useOutlookProjection,
    useOutlookTemplates,
    useTemplateCandidates,
    type JournalEntryTemplate,
    type OutlookAccountProjection,
    type TemplateCandidate,
    type WireScenarioRequest,
} from '../api/outlook';
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
import { cx } from '../lib/cx';
import { type JournalEntryTemplateId } from '../lib/domain';
import { JournalEntryTemplateForm } from './JournalEntryTemplateForm';
import { OutlookProjectionChart } from './OutlookProjectionChart';

const CURRENCY = 'EUR';
const HORIZONS = [6, 12, 24] as const;

function formatDueDate(iso: string): string {
    const [y, m, d] = iso.split('-').map(Number);
    return new Date(y ?? 1970, (m ?? 1) - 1, d ?? 1).toLocaleDateString(undefined, {
        day: 'numeric',
        month: 'short',
        year: 'numeric',
    });
}

export function Outlook() {
    const [horizon, setHorizon] = useState<number>(12);
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
                horizon={horizon}
                onHorizon={setHorizon}
                hasScenario={scenario !== null}
                onResetScenario={() => {
                    setDisabledIds(new Set());
                }}
            />

            <section className="grid gap-[18px] grid-cols-1 lg:grid-cols-[1.4fr_1fr] mt-[18px]">
                <RecurringPanel
                    disabledIds={disabledIds}
                    onToggleDisabled={toggleDisabled}
                    onAdd={() => {
                        setCreating(true);
                    }}
                    onEdit={setEditing}
                />
                <CandidatesPanel onAccept={setAccepting} />
            </section>

            {creating && (
                <JournalEntryTemplateForm
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
    horizon,
    onHorizon,
    hasScenario,
    onResetScenario,
}: {
    projection: ReturnType<typeof useOutlookProjection>;
    horizon: number;
    onHorizon: (h: number) => void;
    hasScenario: boolean;
    onResetScenario: () => void;
}) {
    const { t } = useLingui();
    const [selectedId, setSelectedId] = useState<string | null>(null);

    const accounts = projection.data?.accounts ?? [];
    const selected = accounts.find(a => a.accountId === selectedId) ?? accounts[0] ?? null;

    return (
        <Panel>
            <SectionHead
                title={<Trans>Outlook</Trans>}
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
                                    setSelectedId(a.accountId);
                                }}
                                className={cx(
                                    'px-3 py-1.5 rounded-lg text-sm font-medium transition-colors',
                                    a.accountId === selected?.accountId
                                        ? 'bg-brand-primary-soft text-brand-primary'
                                        : 'text-fg-2 hover:bg-surface-2',
                                )}
                            >
                                {a.accountName}
                            </button>
                        ))}
                    </div>

                    {selected && <ProjectionDetail account={selected} horizon={horizon} />}
                </div>
            )}
        </Panel>
    );
}

function ProjectionDetail({
    account,
    horizon,
}: {
    account: OutlookAccountProjection;
    horizon: number;
}) {
    const endMid = account.baseline.at(-1)?.endBalanceMid ?? account.currentBalance;
    const endLow = account.baseline.at(-1)?.endBalanceLow ?? account.currentBalance;
    const endHigh = account.baseline.at(-1)?.endBalanceHigh ?? account.currentBalance;
    const scenarioEnd = account.scenario?.at(-1)?.endBalanceMid ?? null;

    return (
        <>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                <Kpi label={<Trans>Now</Trans>}>
                    <Amount
                        minor={account.currentBalance}
                        currencyCode={account.currencyCode}
                        size="medium"
                    />
                </Kpi>
                <Kpi label={<Trans>In {horizon} months</Trans>}>
                    <Amount minor={endMid} currencyCode={account.currencyCode} size="medium" />
                    <span className="text-xs text-fg-3 mt-0.5">
                        <Amount
                            minor={endLow}
                            currencyCode={account.currencyCode}
                            size="inline"
                            decimals={false}
                        />
                        {' – '}
                        <Amount
                            minor={endHigh}
                            currencyCode={account.currencyCode}
                            size="inline"
                            decimals={false}
                        />
                    </span>
                </Kpi>
                {scenarioEnd !== null && (
                    <Kpi label={<Trans>What-if</Trans>}>
                        <span className="text-[#f59e0b]">
                            <Amount
                                minor={scenarioEnd}
                                currencyCode={account.currencyCode}
                                size="medium"
                            />
                        </span>
                    </Kpi>
                )}
            </div>

            <OutlookProjectionChart account={account} />
        </>
    );
}

function Kpi({ label, children }: { label: React.ReactNode; children: React.ReactNode }) {
    return (
        <div className="flex flex-col gap-1 p-3 rounded-xl bg-surface-2">
            <span className="text-xs font-medium text-fg-3 uppercase tracking-wide">{label}</span>
            <span className="flex flex-col">{children}</span>
        </div>
    );
}

function RecurringPanel({
    disabledIds,
    onToggleDisabled,
    onAdd,
    onEdit,
}: {
    disabledIds: Set<JournalEntryTemplateId>;
    onToggleDisabled: (id: JournalEntryTemplateId) => void;
    onAdd: () => void;
    onEdit: (template: JournalEntryTemplate) => void;
}) {
    const { t } = useLingui();
    const templates = useOutlookTemplates();
    const remove = useDeleteTemplate();
    const toast = useToast();

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
                subtitle={<Trans>Expected payments and income that shape the projection.</Trans>}
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
            ) : templates.data.length === 0 ? (
                <Empty
                    title={t`No recurring items yet`}
                    hint={t`Add your rent, salary, and subscriptions — or accept a detected one.`}
                />
            ) : (
                <div className="flex flex-col divide-y divide-border-soft">
                    {templates.data.map(template => (
                        <TemplateRow
                            key={template.id}
                            template={template}
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
    disabled,
    onToggleDisabled,
    onEdit,
    onDelete,
}: {
    template: JournalEntryTemplate;
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
                    {template.accountName}
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

function CandidatesPanel({ onAccept }: { onAccept: (candidate: TemplateCandidate) => void }) {
    const { t } = useLingui();
    const candidates = useTemplateCandidates();

    return (
        <Panel>
            <SectionHead
                title={<Trans>Detected</Trans>}
                subtitle={<Trans>Patterns we spotted in your history.</Trans>}
            />

            {candidates.isPending ? (
                <Skeleton className="h-24" />
            ) : candidates.isError ? (
                <ErrorState
                    message={t`Couldn't load suggestions.`}
                    onRetry={() => void candidates.refetch()}
                />
            ) : candidates.data.length === 0 ? (
                <Empty
                    title={t`Nothing detected`}
                    hint={t`We'll suggest recurring items as your history grows.`}
                />
            ) : (
                <div className="flex flex-col divide-y divide-border-soft">
                    {candidates.data.map((candidate, i) => (
                        <div key={i} className="flex items-center gap-3 py-3">
                            <div className="flex-1 min-w-0">
                                <div className="text-sm font-medium text-fg-1 truncate">
                                    {candidate.suggestedName}
                                </div>
                                <div className="text-xs text-fg-3 truncate">
                                    {candidate.accountName} · {candidate.cadence} ·{' '}
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
