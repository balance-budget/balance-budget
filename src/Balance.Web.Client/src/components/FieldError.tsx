type FieldErrorProps = {
    name: string;
    errors: Record<string, string[]> | null | undefined;
};

/**
 * Renders the first server-side validation message for `name` if present.
 * Field paths match what FluentValidation emits (typically PascalCase property
 * names); the lookup is case-insensitive against the keys in the errors dict.
 */
export function FieldError({ name, errors }: FieldErrorProps) {
    if (!errors) return null;
    const message = pickMessage(errors, name);
    if (!message) return null;
    return <p className="text-12 text-danger mt-1">{message}</p>;
}

function pickMessage(errors: Record<string, string[]>, name: string): string | null {
    const direct = errors[name];
    if (direct && direct.length > 0) return direct[0] ?? null;
    const lower = name.toLowerCase();
    for (const key of Object.keys(errors)) {
        if (key.toLowerCase() === lower) {
            const msgs = errors[key];
            if (msgs && msgs.length > 0) return msgs[0] ?? null;
        }
    }
    return null;
}
