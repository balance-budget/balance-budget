import type { Key, Selection } from 'react-aria-components';

/** First key of a single-select `Selection` (`'all'` never applies to these). */
export function selectedKey(selection: Selection): Key | undefined {
    if (selection === 'all') return undefined;
    for (const key of selection) return key;
    return undefined;
}
