import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { Currencies } from '../screens/Currencies';

export const Route = createFileRoute('/settings/currencies')({
    component: Currencies,
    staticData: { title: msg`Currencies` },
});
