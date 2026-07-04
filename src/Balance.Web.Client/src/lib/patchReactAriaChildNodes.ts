/*
 * Patch React Aria's `CollectionNode.childNodes` getter so it returns an empty
 * array instead of throwing.
 *
 * React 19.2's dev-mode profiling (logComponentRender → addObjectDiffToProperties)
 * walks every enumerable property of a component's props when diffing renders,
 * including getters. React Aria's collection nodes expose a `childNodes` getter
 * that throws by design ("childNodes is not supported"). The thrown error aborts
 * useComboBoxState's effect, which breaks the ComboBox's internal state tracking
 * and resets the input value on every keystroke when filtering static/grouped
 * children.
 *
 * This is the workaround from adobe/react-spectrum#9405 (see also
 * gohypergiant/standard-toolkit#1046). We import `CollectionNode` from the same
 * private subpath `react-aria-components` itself uses, so we patch the exact
 * prototype the components rely on. Applying it lets us track react/react-dom
 * 19.2.x again instead of pinning to the 19.1 backport line.
 *
 * Remove this once the upstream fix ships (react-spectrum#9405 /
 * facebook/react#35126).
 */

// Private subpath that react-aria-components itself imports from internally.
import { CollectionNode } from 'react-aria/private/collections/BaseCollection';

const descriptor = Object.getOwnPropertyDescriptor(CollectionNode.prototype, 'childNodes');
if (descriptor?.get) {
    Object.defineProperty(CollectionNode.prototype, 'childNodes', {
        get() {
            return [];
        },
        configurable: true,
    });
}
