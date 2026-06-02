import {
    createContext,
    useCallback,
    useContext,
    useEffect,
    useMemo,
    useRef,
    useState,
    type ReactNode,
} from 'react';
import { Icon } from './Icon';
import { cx } from '../lib/cx';

type ToastVariant = 'success' | 'error' | 'info';

type Toast = {
    id: number;
    message: string;
    variant: ToastVariant;
};

type ToastApi = {
    push: (message: string, variant?: ToastVariant) => void;
    success: (message: string) => void;
    error: (message: string) => void;
};

const ToastContext = createContext<ToastApi | null>(null);

const DISMISS_MS = 3000;

export function ToastProvider({ children }: { children: ReactNode }) {
    const [toasts, setToasts] = useState<Toast[]>([]);
    const nextId = useRef(1);
    const timers = useRef(new Map<number, ReturnType<typeof setTimeout>>());

    const dismiss = useCallback((id: number) => {
        setToasts(current => current.filter(t => t.id !== id));
        const timer = timers.current.get(id);
        if (timer) {
            clearTimeout(timer);
            timers.current.delete(id);
        }
    }, []);

    const push = useCallback(
        (message: string, variant: ToastVariant = 'info') => {
            const id = nextId.current++;
            setToasts(current => [...current, { id, message, variant }]);
            const timer = setTimeout(() => {
                dismiss(id);
            }, DISMISS_MS);
            timers.current.set(id, timer);
        },
        [dismiss],
    );

    const api = useMemo<ToastApi>(
        () => ({
            push,
            success: msg => {
                push(msg, 'success');
            },
            error: msg => {
                push(msg, 'error');
            },
        }),
        [push],
    );

    useEffect(() => {
        const map = timers.current;
        return () => {
            for (const timer of map.values()) clearTimeout(timer);
            map.clear();
        };
    }, []);

    return (
        <ToastContext.Provider value={api}>
            {children}
            <ToastViewport toasts={toasts} onDismiss={dismiss} />
        </ToastContext.Provider>
    );
}

// eslint-disable-next-line react-refresh/only-export-components -- hook lives alongside provider; splitting would just trade a refresh hint for an extra file.
export function useToast(): ToastApi {
    const ctx = useContext(ToastContext);
    if (!ctx) throw new Error('useToast must be used inside ToastProvider');
    return ctx;
}

const VARIANT_CLASS: Record<ToastVariant, string> = {
    success: 'bg-success-soft text-success border-success/30',
    error: 'bg-danger-soft text-danger border-danger/30',
    info: 'bg-surface-2 text-fg-1 border-border-soft',
};

const VARIANT_ICON: Record<ToastVariant, string> = {
    success: 'check-circle',
    error: 'alert-circle',
    info: 'info',
};

function ToastViewport({
    toasts,
    onDismiss,
}: {
    toasts: Toast[];
    onDismiss: (id: number) => void;
}) {
    return (
        <div
            aria-live="polite"
            className="fixed top-4 right-4 z-50 flex flex-col gap-2 pointer-events-none"
        >
            {toasts.map(t => (
                <button
                    key={t.id}
                    type="button"
                    onClick={() => {
                        onDismiss(t.id);
                    }}
                    className={cx(
                        'pointer-events-auto flex items-center gap-2 px-3 py-2 rounded-sm border',
                        'text-13 font-medium shadow-overlay min-w-[240px] max-w-[360px] text-left',
                        VARIANT_CLASS[t.variant],
                    )}
                >
                    <Icon name={VARIANT_ICON[t.variant]} size={16} strokeWidth={2} />
                    <span className="flex-1">{t.message}</span>
                </button>
            ))}
        </div>
    );
}
