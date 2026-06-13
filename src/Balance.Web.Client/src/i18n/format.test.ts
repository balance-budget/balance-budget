import { afterEach, describe, expect, it } from 'vitest';
import {
    activeDecimalSeparator,
    formatCalendarDateWith,
    formatInstant,
    groupInteger,
    previewNumber,
    setActiveLanguage,
    setActiveRegion,
} from './format';
import { DEFAULT_REGION, resolveRegion } from './region';

const NNBSP = '\u202F';
const DATE = '2026-06-13'; // a Saturday

afterEach(() => {
    setActiveRegion(DEFAULT_REGION);
    setActiveLanguage('en');
});

describe('formatCalendarDateWith — ISO mode (deterministic, language-independent)', () => {
    for (const language of ['en', 'en-GB', 'nl-NL', 'zh-TW']) {
        it(`renders bare numeric ISO regardless of language (${language})`, () => {
            expect(formatCalendarDateWith(language, 'iso', DATE, 'year-month-day')).toBe(
                '2026-06-13',
            );
            expect(formatCalendarDateWith(language, 'iso', DATE, 'year-month')).toBe('2026-06');
            expect(formatCalendarDateWith(language, 'iso', DATE, 'year')).toBe('2026');
            expect(formatCalendarDateWith(language, 'iso', DATE, 'month')).toBe('06');
            expect(formatCalendarDateWith(language, 'iso', DATE, 'month-day')).toBe('06-13');
        });
    }

    it('ignores style and weekday in ISO mode', () => {
        expect(
            formatCalendarDateWith('en', 'iso', DATE, 'year-month-day', {
                style: 'long',
                weekday: true,
            }),
        ).toBe('2026-06-13');
    });

    it('accepts short YYYY / YYYY-MM inputs', () => {
        expect(formatCalendarDateWith('en', 'iso', '2026-06', 'year-month')).toBe('2026-06');
        expect(formatCalendarDateWith('en', 'iso', '2026', 'year')).toBe('2026');
    });
});

describe('formatCalendarDateWith — locale mode (order + month name follow language)', () => {
    it('en is American month-first', () => {
        expect(formatCalendarDateWith('en', 'locale', DATE, 'year-month-day')).toBe('Jun 13, 2026');
    });

    it('en-GB is day-first with an English month name', () => {
        expect(formatCalendarDateWith('en-GB', 'locale', DATE, 'year-month-day')).toBe(
            '13 Jun 2026',
        );
    });

    it('nl-NL is day-first with a Dutch month name', () => {
        const out = formatCalendarDateWith('nl-NL', 'locale', DATE, 'year-month-day');
        expect(out).toMatch(/^13\b/);
        expect(out.toLowerCase()).toContain('jun');
        expect(out).toContain('2026');
    });

    it('zh-TW uses year-month-day order with Chinese month', () => {
        const out = formatCalendarDateWith('zh-TW', 'locale', DATE, 'year-month-day');
        expect(out).toContain('2026');
        expect(out).toContain('6月');
        expect(out).toContain('13');
    });

    it('month+year heading goes long; a present day forces short month', () => {
        expect(formatCalendarDateWith('en', 'locale', DATE, 'year-month', { style: 'long' })).toBe(
            'June 2026',
        );
        // style long is ignored when a day is present (rubric: day ⇒ short).
        expect(
            formatCalendarDateWith('en', 'locale', DATE, 'year-month-day', { style: 'long' }),
        ).toBe('Jun 13, 2026');
    });

    it('weekday is prepended only when requested', () => {
        const out = formatCalendarDateWith('en', 'locale', DATE, 'year-month-day', {
            weekday: true,
        });
        expect(out).toContain('Jun 13, 2026');
        expect(out).not.toBe('Jun 13, 2026'); // has a weekday prefix
    });
});

describe('numbers', () => {
    it('ISO uses narrow no-break space groups and a dot decimal', () => {
        expect(previewNumber('en', 'iso')).toBe(`1${NNBSP}234${NNBSP}567.89`);
        // Language is irrelevant in ISO mode.
        expect(previewNumber('nl-NL', 'iso')).toBe(`1${NNBSP}234${NNBSP}567.89`);
    });

    it('locale defers grouping + decimal to the language', () => {
        expect(previewNumber('en', 'locale')).toBe('1,234,567.89');
        expect(previewNumber('nl-NL', 'locale')).toBe('1.234.567,89');
    });

    it('groupInteger follows the active number preference', () => {
        setActiveRegion({ dateFormat: 'iso', numberFormat: 'iso' });
        expect(groupInteger(1234567)).toBe(`1${NNBSP}234${NNBSP}567`);

        setActiveRegion({ dateFormat: 'iso', numberFormat: 'locale' });
        setActiveLanguage('en');
        expect(groupInteger(1234567)).toBe('1,234,567');
    });

    it('activeDecimalSeparator is a dot in ISO mode, language-driven otherwise', () => {
        setActiveRegion({ dateFormat: 'iso', numberFormat: 'iso' });
        expect(activeDecimalSeparator()).toBe('.');

        setActiveRegion({ dateFormat: 'iso', numberFormat: 'locale' });
        setActiveLanguage('nl-NL');
        expect(activeDecimalSeparator()).toBe(',');
    });
});

describe('formatInstant', () => {
    it('ISO mode renders YYYY-MM-DD HH:mm (24h)', () => {
        setActiveRegion({ dateFormat: 'iso', numberFormat: 'iso' });
        expect(formatInstant('2026-06-13T14:30:00Z')).toMatch(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/);
    });

    it('locale mode includes the year and a time', () => {
        setActiveRegion({ dateFormat: 'locale', numberFormat: 'locale' });
        setActiveLanguage('en');
        const out = formatInstant('2026-06-13T14:30:00Z');
        expect(out).toContain('2026');
        expect(out).toMatch(/\d{1,2}:\d{2}/);
    });
});

describe('resolveRegion — legacy token migration', () => {
    it('maps retired ADR-0022 tokens to locale', () => {
        expect(resolveRegion('dmy', 'comma-dot')).toEqual({
            dateFormat: 'locale',
            numberFormat: 'locale',
        });
        expect(resolveRegion('mdy', 'space-comma')).toEqual({
            dateFormat: 'locale',
            numberFormat: 'locale',
        });
    });

    it('passes through current tokens', () => {
        expect(resolveRegion('iso', 'iso')).toEqual({ dateFormat: 'iso', numberFormat: 'iso' });
        expect(resolveRegion('locale', 'locale')).toEqual({
            dateFormat: 'locale',
            numberFormat: 'locale',
        });
    });

    it('falls back to the ISO default for null/unknown', () => {
        expect(resolveRegion(null, undefined)).toEqual(DEFAULT_REGION);
        expect(resolveRegion('garbage', 'garbage')).toEqual(DEFAULT_REGION);
    });
});
