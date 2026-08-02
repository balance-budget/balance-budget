// @vitest-environment jsdom
import { describe, expect, it, vi } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen, testRouter } from '../../test-utils';
import { RowLink } from './RowLink';
import { Cell, Column, Row, Table, TableBody, TableHeader } from './Table';

/*
 * The anchor contract from ADR-0040: a navigating row exposes a real <a href> the
 * browser can right-click, preview, and open in a new tab. React Aria renders the
 * row itself as role="row" with a synthetic link, so the anchor in the row-header
 * cell is the only thing that makes those native affordances work — and a plain
 * assertion that the row navigates would not catch its loss.
 */

const HREF = { to: '/journal/$id', params: { id: '7' } } as const;

function Harness() {
    return (
        <Table aria-label="Entries">
            <TableHeader>
                <Column isRowHeader>Description</Column>
            </TableHeader>
            <TableBody>
                <Row id="7" href={HREF}>
                    <Cell>
                        <RowLink href={HREF}>Groceries</RowLink>
                    </Cell>
                </Row>
            </TableBody>
        </Table>
    );
}

describe('navigating rows', () => {
    it('renders a real anchor with the resolved URL', () => {
        render(<Harness />);

        const link = screen.getByRole('link', { name: 'Groceries' });

        expect(link.tagName).toBe('A');
        expect(link.getAttribute('href')).toBe('/journal/7');
    });

    it('navigates once when the anchor is clicked, not twice', async () => {
        const navigate = vi.spyOn(testRouter, 'navigate').mockResolvedValue(undefined);
        render(<Harness />);

        // The anchor sits inside a row that is itself a link target, so a single
        // click must not be handled by both.
        await userEvent.click(screen.getByRole('link', { name: 'Groceries' }));

        expect(navigate).toHaveBeenCalledTimes(1);
        expect(navigate).toHaveBeenCalledWith(expect.objectContaining({ to: '/journal/$id' }));
        navigate.mockRestore();
    });
});
