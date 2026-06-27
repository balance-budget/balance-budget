// @vitest-environment jsdom
import { useState } from 'react';
import { type Selection } from 'react-aria-components';
import { render } from '../../test-utils';
import { User } from '@react-aria/test-utils';
import { describe, expect, it } from 'vitest';
import { Cell, Column, Row, Table, TableBody, TableHeader } from './Table';

/*
 * App wiring on top of React Aria's Table that the pure helpers can't cover:
 * that disabledKeys + disabledBehavior="selection" make select-all skip the
 * disabled rows (the "only Uncleared rows are selectable" rule, ADR-0035).
 * React Aria's own selection/keyboard machinery is not re-tested here.
 */

const testUtilUser = new User({ interactionType: 'mouse' });

const ROWS = [
    { id: '1', label: 'One' },
    { id: '2', label: 'Two (disabled)' },
    { id: '3', label: 'Three' },
];

function Harness() {
    const [selected, setSelected] = useState<Selection>(new Set());
    return (
        <Table
            aria-label="Rows"
            selectionMode="multiple"
            disabledBehavior="selection"
            disabledKeys={['2']}
            selectedKeys={selected}
            onSelectionChange={keys => {
                // Mirror the screens: "all" expands to the selectable rows only.
                setSelected(keys === 'all' ? new Set(['1', '3']) : keys);
            }}
        >
            <TableHeader>
                <Column isRowHeader>Label</Column>
            </TableHeader>
            <TableBody items={ROWS}>
                {row => (
                    <Row>
                        <Cell>{row.label}</Cell>
                    </Row>
                )}
            </TableBody>
        </Table>
    );
}

describe('Table select-all wiring', () => {
    it('select-all skips disabled rows', async () => {
        const view = render(<Harness />);
        const table = testUtilUser.createTester('Table', { root: view.container });

        await table.toggleSelectAll();

        expect(table.getSelectedRows()).toHaveLength(2);
    });
});
