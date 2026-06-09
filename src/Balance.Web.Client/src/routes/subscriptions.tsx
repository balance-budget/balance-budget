import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { Empty } from '../components/Empty';
import { i18n } from '../i18n/i18n';

export const Route = createFileRoute('/subscriptions')({
    component: () => <Empty title={i18n._(msg`Subscriptions`)} />,
    staticData: { title: msg`Subscriptions` },
});
