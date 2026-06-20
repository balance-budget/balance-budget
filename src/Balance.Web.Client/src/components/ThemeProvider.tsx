import {
    createContext,
    useCallback,
    useContext,
    useEffect,
    useMemo,
    useRef,
    useState,
} from 'react';
import { useCurrentUser, useUpdatePreferences } from '../api/auth';
import {
    applyResolvedTheme,
    darkMediaQuery,
    isThemePreference,
    readStoredPreference,
    resolveTheme,
    writeStoredPreference,
    type ResolvedTheme,
    type ThemePreference,
} from '../lib/theme';

type ThemeContextValue = {
    /** The durable tri-state choice. */
    preference: ThemePreference;
    /** What the DOM is actually rendering. */
    resolved: ResolvedTheme;
    /** Set the preference; persists locally and (when signed in) server-side. */
    setPreference: (preference: ThemePreference) => void;
    /** Topbar quick action: flip to the explicit opposite of the resolved theme. */
    toggle: () => void;
};

const ThemeContext = createContext<ThemeContextValue | null>(null);

/**
 * Single owner of theme state (ADR-0031). Holds the preference, keeps the DOM
 * `.dark` class + meta theme-color in sync, follows the OS while on `auto`, and
 * persists changes to localStorage and the server. The server is the source of
 * truth and wins on first load (cross-device reconcile); thereafter the provider
 * is authoritative and pushes changes outward.
 */
export function ThemeProvider({ children }: { children: React.ReactNode }) {
    const { data: user } = useCurrentUser();
    const savePreferences = useUpdatePreferences();

    const [preference, setPreferenceState] = useState<ThemePreference>(() =>
        readStoredPreference(),
    );
    const [resolved, setResolved] = useState<ResolvedTheme>(() => resolveTheme(preference));

    // A ref mirror so the matchMedia listener reads the current preference
    // without resubscribing on every change.
    const preferenceRef = useRef(preference);
    useEffect(() => {
        preferenceRef.current = preference;
    }, [preference]);

    const apply = useCallback((next: ThemePreference) => {
        const nextResolved = resolveTheme(next);
        setResolved(nextResolved);
        applyResolvedTheme(nextResolved);
    }, []);

    // Keep the DOM in sync with the resolved theme (the boot script set the
    // initial value; this covers later changes and the meta tag).
    useEffect(() => {
        applyResolvedTheme(resolved);
    }, [resolved]);

    // Follow the OS, but only while the preference is `auto`.
    useEffect(() => {
        const media = darkMediaQuery();
        const onChange = () => {
            if (preferenceRef.current === 'auto') apply('auto');
        };
        media.addEventListener('change', onChange);
        return () => {
            media.removeEventListener('change', onChange);
        };
    }, [apply]);

    const setPreference = useCallback(
        (next: ThemePreference) => {
            setPreferenceState(next);
            apply(next);
            writeStoredPreference(next);
            if (user) {
                // PUT is replace-semantics — resend the other prefs so they survive.
                savePreferences.mutate({
                    language: user.language,
                    dateFormat: user.dateFormat,
                    numberFormat: user.numberFormat,
                    theme: next,
                });
            }
        },
        [apply, savePreferences, user],
    );

    const toggle = useCallback(() => {
        setPreference(resolved === 'dark' ? 'light' : 'dark');
    }, [resolved, setPreference]);

    // Reconcile from the server once per signed-in user: the stored value may
    // be stale relative to a change made on another device, so the server wins
    // on load. We reconcile a given user only once so a later local change
    // isn't reverted by the stale echo before the save round-trips.
    const reconciledUserRef = useRef<string | null>(null);
    useEffect(() => {
        if (!user) {
            reconciledUserRef.current = null;
            return;
        }
        if (reconciledUserRef.current === user.id) return;
        reconciledUserRef.current = user.id;

        const serverPreference = isThemePreference(user.theme) ? user.theme : null;
        if (serverPreference && serverPreference !== preferenceRef.current) {
            setPreferenceState(serverPreference);
            apply(serverPreference);
            writeStoredPreference(serverPreference);
        }
    }, [user, apply]);

    const value = useMemo<ThemeContextValue>(
        () => ({ preference, resolved, setPreference, toggle }),
        [preference, resolved, setPreference, toggle],
    );

    return <ThemeContext value={value}>{children}</ThemeContext>;
}

// eslint-disable-next-line react-refresh/only-export-components -- the hook lives alongside its provider; splitting would just trade a refresh hint for an extra file.
export function useTheme(): ThemeContextValue {
    const value = useContext(ThemeContext);
    if (!value) throw new Error('useTheme must be used within a ThemeProvider');
    return value;
}
