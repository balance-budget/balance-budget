import { useLingui } from '@lingui/react/macro';

type EmptyProps = {
    title: string;
    hint?: string;
};

export function Empty({ title, hint }: EmptyProps) {
    const { t } = useLingui();
    return (
        <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
                <h2 className="text-3xl font-medium text-fg-1">{title}</h2>
                <p className="mt-2 text-sm text-fg-3">{hint ?? t`This screen is not built yet.`}</p>
            </div>
        </div>
    );
}
