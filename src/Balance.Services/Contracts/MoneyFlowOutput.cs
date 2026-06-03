using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// The <b>Money flow</b> report (ADR-0023): the whole ledger's in/out picture over a <b>Reporting
/// period</b> as a single-hub flow diagram, ready to render as a Sankey. Every account contributes
/// one flow sized by its <b>Net movement</b>; its side is chosen by sign. Sources (money in) link
/// <c>node → hub</c>; exits (money out) link <c>hub → node</c>. Sources and exits balance exactly by
/// the double-entry identity (within one currency the signed line sums net to zero), so the diagram
/// always closes with no plug node. Income/Expense render at full chart-of-accounts depth (subtrees
/// become intermediate nodes); balance-sheet accounts render at top level only (v1).
/// </summary>
public sealed record MoneyFlowOutput(
    DateOnly From,
    DateOnly To,
    CurrencyCode CurrencyCode,
    IReadOnlyList<MoneyFlowNode> Nodes,
    IReadOnlyList<MoneyFlowLink> Links
);

/// <summary>
/// One node in the <see cref="MoneyFlowOutput"/>. <see cref="Id"/> is the account's id as a string,
/// or <c>"hub"</c> for the central node. <see cref="Kind"/> drives styling: the hub is distinct, and
/// account nodes carry their <c>AccountType</c> so the renderer can colour income/expense vs the
/// balance sheet.
/// </summary>
public sealed record MoneyFlowNode(string Id, string Name, MoneyFlowNodeKind Kind);

/// <summary>
/// One directed flow between two <see cref="MoneyFlowNode"/>s. <see cref="Value"/> is always a
/// positive magnitude (the absolute Net movement on that edge); direction encodes in/out.
/// </summary>
public sealed record MoneyFlowLink(string Source, string Target, Money Value);

public enum MoneyFlowNodeKind
{
    Hub,
    Income,
    Expense,
    Asset,
    Liability,
    Equity,
}
