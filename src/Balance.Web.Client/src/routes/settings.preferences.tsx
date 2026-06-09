import { createFileRoute } from '@tanstack/react-router';
import { Preferences } from '../screens/Preferences';

export const Route = createFileRoute('/settings/preferences')({
    component: Preferences,
    staticData: { title: 'Preferences' },
});
