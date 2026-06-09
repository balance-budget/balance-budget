import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { BankImports } from '../screens/BankImports';

export const Route = createFileRoute('/bank-imports')({
    component: BankImports,
    staticData: { title: msg`Bank imports` },
});
