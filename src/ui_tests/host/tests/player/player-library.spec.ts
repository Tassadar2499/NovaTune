import { expect, test } from '@playwright/test';
import { registerPlayer } from '../support/auth.js';
import { createUser } from '../support/runtime.js';

test.describe('Player library', () => {
  test('shows the empty library state for a new listener account', async ({ page }) => {
    await registerPlayer(page, createUser('Player Library'));

    await expect(page.getByTestId('library-heading')).toBeVisible();
    await expect(page.getByTestId('empty-state')).toContainText('No tracks found');
    await expect(page.getByRole('link', { name: 'Upload Track' })).toBeVisible();
  });
});
