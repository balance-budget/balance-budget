import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';

/** One step in the TopBar breadcrumb — a parent section the current page sits under. */
export type Crumb = { label: string; to: string };

/** A page's contextual header. `title` overrides the route's static title with a
 *  specific one (e.g. an account name); `breadcrumb` is the trail of parent
 *  sections shown above it. Both are optional — section pages need neither. */
export type PageHeader = { title?: string; breadcrumb?: Crumb[] };

type PageHeaderContextValue = {
    header: PageHeader | null;
    setHeader: (header: PageHeader | null) => void;
};

const PageHeaderContext = createContext<PageHeaderContextValue | null>(null);

export function PageHeaderProvider({ children }: { children: ReactNode }) {
    const [header, setHeader] = useState<PageHeader | null>(null);
    return (
        <PageHeaderContext.Provider value={{ header, setHeader }}>
            {children}
        </PageHeaderContext.Provider>
    );
}

/** Read the active contextual header (used by the shell to render the TopBar). */
export function usePageHeaderValue(): PageHeader | null {
    return useContext(PageHeaderContext)?.header ?? null;
}

/** Drive the TopBar's specific title + breadcrumb from a screen. The header
 *  resets when the screen unmounts (route change) so a stale title never lingers
 *  behind the next page. Keyed on the serialized header, so passing a fresh
 *  object literal each render is fine as long as its contents are stable. */
export function usePageHeader(header: PageHeader): void {
    const ctx = useContext(PageHeaderContext);
    const setHeader = ctx?.setHeader;
    const key = JSON.stringify(header);
    useEffect(() => {
        if (!setHeader) return;
        setHeader(header);
        return () => {
            setHeader(null);
        };
        // `header` is reconstructed from `key`; depending on `key` avoids churn
        // from a new-but-equal object identity each render.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [setHeader, key]);
}
