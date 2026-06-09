// @vitest-environment jsdom
import { render, screen } from '../test-utils';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { presetRange } from '../lib/reportPeriod';
import { PeriodPicker } from './PeriodPicker';

/*
 * Preset ↔ custom interplay: pressing a preset pill emits its computed range,
 * and a period that matches no preset leaves every pill unselected. The
 * DateRangePicker internals are React Aria's own.
 */

describe('PeriodPicker', () => {
    it('emits the preset range when a pill is pressed', async () => {
        const onChange = vi.fn();
        render(
            <PeriodPicker period={{ from: '2026-01-01', to: '2026-01-31' }} onChange={onChange} />,
        );
        await userEvent.click(screen.getByRole('radio', { name: 'This month' }));
        expect(onChange).toHaveBeenCalledWith(presetRange('this-month'));
    });

    it('marks the matching preset pill as selected', () => {
        render(<PeriodPicker period={presetRange('this-year')} onChange={vi.fn()} />);
        const pill = screen.getByRole('radio', { name: 'This year' });
        expect(pill.getAttribute('data-selected')).toBe('true');
    });

    it('selects no pill for a custom period', () => {
        render(
            <PeriodPicker period={{ from: '2026-01-03', to: '2026-01-19' }} onChange={vi.fn()} />,
        );
        const pills = screen.getAllByRole('radio');
        expect(pills.length).toBeGreaterThan(0);
        for (const pill of pills) {
            expect(pill.getAttribute('data-selected')).toBeNull();
        }
    });
});
