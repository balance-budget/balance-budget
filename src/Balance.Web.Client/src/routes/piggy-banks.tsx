import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { Empty } from '../components/Empty';
import { i18n } from '../i18n/i18n';

export const Route = createFileRoute('/piggy-banks')({
    component: () => <Empty title={i18n._(msg`Piggy banks`)} />,
    staticData: { title: msg`Piggy banks` },
});
