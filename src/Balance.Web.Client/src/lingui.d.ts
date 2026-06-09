// PO catalogs are compiled to message objects on import by @lingui/vite-plugin.
declare module '*.po' {
    import type { Messages } from '@lingui/core';
    export const messages: Messages;
}
