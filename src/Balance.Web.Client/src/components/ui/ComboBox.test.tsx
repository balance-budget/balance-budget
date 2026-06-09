// @vitest-environment jsdom
import { render } from '../../test-utils';
import userEvent from '@testing-library/user-event';
import { User } from '@react-aria/test-utils';
import { describe, expect, it, vi } from 'vitest';
import { ComboBox } from './ComboBox';
import type { ComboBoxItem } from './combobox.state';

/*
 * App-specific ComboBox behavior on top of React Aria's ComboBox: searchText
 * matching, group ordering, and the None/Create sentinel rows. React Aria's
 * own keyboard/ARIA machinery is not re-tested here (Adobe covers that).
 */

const testUtilUser = new User({ interactionType: 'mouse' });

function accountishItems(): ComboBoxItem<string>[] {
    return [
        {
            key: 'car-tax',
            label: '5110 Car › Tax',
            searchText: '5110 Car Tax',
            group: 'Expense',
            value: 'car-tax',
        },
        {
            key: 'salary',
            label: '4100 Salary',
            searchText: '4100 Salary',
            group: 'Income',
            value: 'salary',
        },
        {
            key: 'home-tax',
            label: '5210 Home › Tax',
            searchText: '5210 Home Tax',
            group: 'Expense',
            value: 'home-tax',
        },
    ];
}

function renderComboBox(overrides: Partial<Parameters<typeof ComboBox<string>>[0]> = {}) {
    const onChange = vi.fn();
    const onClear = vi.fn();
    const view = render(
        <ComboBox
            items={accountishItems()}
            value={null}
            onChange={onChange}
            onClear={onClear}
            groupOrder={['Income', 'Expense']}
            ariaLabel="Account"
            {...overrides}
        />,
    );
    const tester = testUtilUser.createTester('ComboBox', {
        root: view.container,
    });
    return { view, tester, onChange, onClear };
}

describe('ComboBox on React Aria', () => {
    it('shows all options grouped in groupOrder when opened', async () => {
        const { tester } = renderComboBox();
        await tester.open();
        const labels = tester.getOptions().map(o => o.textContent);
        expect(labels).toEqual(['4100 Salary', '5110 Car › Tax', '5210 Home › Tax']);
    });

    it('filters by searchText facets the label hides', async () => {
        const { tester } = renderComboBox();
        await tester.open();
        await userEvent.keyboard('car t');
        const labels = tester.getOptions().map(o => o.textContent);
        expect(labels).toEqual(['5110 Car › Tax']);
    });

    it('selecting an option commits the item value', async () => {
        const { tester, onChange } = renderComboBox();
        await tester.open();
        await tester.toggleOptionSelection({ option: '4100 Salary' });
        expect(onChange).toHaveBeenCalledWith('salary');
    });

    it('renders the None sentinel first and routes it to onClear', async () => {
        const { tester, onClear, onChange } = renderComboBox({ noneLabel: '── None' });
        await tester.open();
        expect(tester.getOptions()[0]?.textContent).toBe('── None');
        await tester.toggleOptionSelection({ option: '── None' });
        expect(onClear).toHaveBeenCalledOnce();
        expect(onChange).not.toHaveBeenCalled();
    });

    it('offers Create for unmatched text and passes the trimmed input', async () => {
        const onCreate = vi.fn();
        const { tester } = renderComboBox({ onCreate });
        await tester.open();
        await userEvent.keyboard('  Xeon ');
        await tester.toggleOptionSelection({ option: "Create 'Xeon'" });
        expect(onCreate).toHaveBeenCalledWith('Xeon');
    });

    it('omits Create when the text exactly matches an existing label', async () => {
        const { tester } = renderComboBox({ onCreate: vi.fn() });
        await tester.open();
        await userEvent.keyboard('4100 Salary');
        const labels = tester.getOptions().map(o => o.textContent);
        expect(labels).toEqual(['4100 Salary']);
    });
});
