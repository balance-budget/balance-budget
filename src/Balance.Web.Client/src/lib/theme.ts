/*
 * Theme preference plumbing (ADR-0031). The tri-state *preference*
 * (auto/light/dark) is the durable choice; the *resolved* theme (light/dark)
 * is what the DOM carries via the `.dark` class. `auto` follows the OS through
 * `prefers-color-scheme`.
 *
 * The same vocabulary and storage key are mirrored by the inline boot script in
 * index.html, which applies the theme before React mounts to avoid a flash of
 * the wrong theme. Keep the two in sync.
 */

export type ThemePreference = 'auto' | 'light' | 'dark';
export type ResolvedTheme = 'light' | 'dark';

export const THEME_PREFERENCES: readonly ThemePreference[] = ['auto', 'light', 'dark'];
export const THEME_STORAGE_KEY = 'balance-theme';
export const DEFAULT_THEME_PREFERENCE: ThemePreference = 'auto';

// Surface color for the mobile browser chrome (<meta name="theme-color">).
// Mirrors --color-bg-0 in index.css for each theme.
const THEME_COLOR: Record<ResolvedTheme, string> = {
    light: '#faf8f5',
    dark: '#1b1b1b',
};

export function isThemePreference(value: unknown): value is ThemePreference {
    return value === 'auto' || value === 'light' || value === 'dark';
}

const DARK_QUERY = '(prefers-color-scheme: dark)';

export function prefersDark(): boolean {
    return typeof window !== 'undefined' && window.matchMedia(DARK_QUERY).matches;
}

export function resolveTheme(preference: ThemePreference): ResolvedTheme {
    if (preference === 'auto') return prefersDark() ? 'dark' : 'light';
    return preference;
}

export function darkMediaQuery(): MediaQueryList {
    return window.matchMedia(DARK_QUERY);
}

export function readStoredPreference(): ThemePreference {
    try {
        const raw = localStorage.getItem(THEME_STORAGE_KEY);
        return isThemePreference(raw) ? raw : DEFAULT_THEME_PREFERENCE;
    } catch {
        return DEFAULT_THEME_PREFERENCE;
    }
}

export function writeStoredPreference(preference: ThemePreference): void {
    try {
        localStorage.setItem(THEME_STORAGE_KEY, preference);
    } catch {
        // Private-mode or storage-disabled: the in-memory preference still
        // applies for this session; we just lose persistence.
    }
}

// Reflect the resolved theme into the DOM: the `.dark` class drives every token
// (and `color-scheme` via index.css), and the meta tag colors the mobile chrome.
export function applyResolvedTheme(resolved: ResolvedTheme): void {
    document.documentElement.classList.toggle('dark', resolved === 'dark');

    let meta = document.head.querySelector<HTMLMetaElement>('meta[name="theme-color"]');
    if (!meta) {
        meta = document.createElement('meta');
        meta.name = 'theme-color';
        document.head.appendChild(meta);
    }
    meta.content = THEME_COLOR[resolved];
}
