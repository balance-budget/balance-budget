import { tooltip } from '@tanstack/charts/tooltip';
import { portal } from '@tanstack/charts/tooltip/portal';

/**
 * Spread into `defineChart` to get the shared tooltip behavior: the native
 * tooltip extension, portaled out of the surrounding card so it never clips.
 * The body itself is rendered by `components/Chart`.
 */
export const chartTooltip = { use: tooltip, portal } as const;
