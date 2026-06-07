type EmptyProps = {
    title: string;
    hint?: string;
};

export function Empty({ title, hint = 'This screen is not built yet.' }: EmptyProps) {
    return (
        <div className="flex-1 flex items-center justify-center">
            <div className="text-center">
                <h2 className="text-3xl font-medium text-fg-1">{title}</h2>
                <p className="mt-2 text-sm text-fg-3">{hint}</p>
            </div>
        </div>
    );
}
