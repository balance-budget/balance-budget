// @vitest-environment jsdom
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Form } from 'react-aria-components';
import { describe, expect, it } from 'vitest';
import { TextField } from './TextField';

/*
 * Server-error surfacing: the app routes FluentValidation field errors into
 * React Aria's Form `validationErrors`, keyed by the PascalCase field names
 * the API emits. They must render under the matching field and clear when the
 * user edits it.
 */

describe('Form validationErrors', () => {
    it('renders the server message under the field with the matching name', () => {
        render(
            <Form validationErrors={{ Name: ['Name is already in use.'] }}>
                <TextField label="Name" name="Name" defaultValue="Groceries" />
            </Form>,
        );
        expect(screen.getByText('Name is already in use.')).toBeDefined();
        expect(screen.getByRole('textbox').getAttribute('aria-invalid')).toBe('true');
    });

    it('clears the error once the user edits the field', async () => {
        render(
            <Form validationErrors={{ Name: ['Name is already in use.'] }}>
                <TextField label="Name" name="Name" defaultValue="Groceries" />
            </Form>,
        );
        await userEvent.type(screen.getByRole('textbox'), 'x');
        await userEvent.tab();
        expect(screen.queryByText('Name is already in use.')).toBeNull();
    });
});
