import { useState } from 'react';
import { Form } from 'react-aria-components';
import { Trans, useLingui } from '@lingui/react/macro';
import { useCurrentUser, useUpdatePreferences } from '../api/auth';
import { Panel, SectionHead } from '../components/Panel';
import { Button } from '../components/ui/Button';
import { Select, SelectItem } from '../components/ui/Select';
import { useToast } from '../components/ui/Toast';
import {
    DEFAULT_LANGUAGE,
    LANGUAGE_NAMES,
    SUPPORTED_LANGUAGES,
    isSupportedLanguage,
    type Language,
} from '../i18n/i18n';
import {
    DATE_FORMATS,
    NUMBER_FORMATS,
    dateLocale,
    numberLocale,
    resolveRegion,
    type DateFormatPref,
    type NumberFormatPref,
} from '../i18n/region';

// A fixed sample so each option previews exactly what it produces.
const SAMPLE_DATE = new Date(2026, 2, 9); // 9 March 2026
const SAMPLE_NUMBER = 1234567.89;

function dateSample(pref: DateFormatPref): string {
    return new Intl.DateTimeFormat(dateLocale(pref), {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
    }).format(SAMPLE_DATE);
}

function numberSample(pref: NumberFormatPref): string {
    return new Intl.NumberFormat(numberLocale(pref)).format(SAMPLE_NUMBER);
}

export function Preferences() {
    const { t } = useLingui();
    const { data: user } = useCurrentUser();
    const save = useUpdatePreferences();
    const toast = useToast();

    const current = resolveRegion(user?.dateFormat, user?.numberFormat);
    const currentLanguage: Language = isSupportedLanguage(user?.language)
        ? user.language
        : DEFAULT_LANGUAGE;
    const [language, setLanguage] = useState<Language>(currentLanguage);
    const [dateFormat, setDateFormat] = useState<DateFormatPref>(current.dateFormat);
    const [numberFormat, setNumberFormat] = useState<NumberFormatPref>(current.numberFormat);

    const dirty =
        language !== currentLanguage ||
        dateFormat !== current.dateFormat ||
        numberFormat !== current.numberFormat;

    const dateLabel: Record<DateFormatPref, string> = {
        iso: t`ISO 8601`,
        dmy: t`Day · Month · Year`,
        mdy: t`Month · Day · Year`,
    };

    async function persist() {
        try {
            await save.mutateAsync({
                language,
                dateFormat,
                numberFormat,
            });
            toast.success(t`Display preferences saved.`);
        } catch (err) {
            if (err instanceof Error) {
                toast.error(err.message);
            }
        }
    }

    return (
        <Panel>
            <SectionHead
                title={t`Language & formatting`}
                subtitle={t`How dates and numbers are displayed. The interface language is separate from formatting.`}
            />
            <Form
                onSubmit={e => {
                    e.preventDefault();
                    void persist();
                }}
                className="flex flex-col gap-4 mt-2"
            >
                <Select
                    label={t`Language`}
                    value={language}
                    onChange={key => {
                        setLanguage(key as Language);
                    }}
                    description={t`The interface language. Formatting below is set separately.`}
                >
                    {SUPPORTED_LANGUAGES.map(lang => (
                        <SelectItem key={lang} id={lang}>
                            {LANGUAGE_NAMES[lang]}
                        </SelectItem>
                    ))}
                </Select>

                <Select
                    label={t`Date format`}
                    value={dateFormat}
                    onChange={key => {
                        setDateFormat(key as DateFormatPref);
                    }}
                >
                    {DATE_FORMATS.map(pref => (
                        <SelectItem key={pref} id={pref}>
                            {dateLabel[pref]} · {dateSample(pref)}
                        </SelectItem>
                    ))}
                </Select>

                <Select
                    label={t`Number format`}
                    value={numberFormat}
                    onChange={key => {
                        setNumberFormat(key as NumberFormatPref);
                    }}
                >
                    {NUMBER_FORMATS.map(pref => (
                        <SelectItem key={pref} id={pref}>
                            {numberSample(pref)}
                        </SelectItem>
                    ))}
                </Select>

                <div className="flex justify-end">
                    <Button type="submit" isDisabled={!dirty || save.isPending}>
                        {save.isPending ? <Trans>Saving…</Trans> : <Trans>Save</Trans>}
                    </Button>
                </div>
            </Form>
        </Panel>
    );
}
