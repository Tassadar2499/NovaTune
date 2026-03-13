import { expect, test } from '@playwright/test';
import { createUser } from '../support/runtime.js';
import { loginPlayer, registerPlayer } from '../support/auth.js';

test.describe('Player authentication', () => {
  test('redirects unauthenticated users to login', async ({ page }) => {
    await page.goto('playlists');

    await expect(page).toHaveURL(/auth\/login\?redirect=%2Fplaylists/);
    await expect(page.getByTestId('login-button')).toBeVisible();
  });

  test('registers, logs out, and logs back in as a listener', async ({ page }) => {
    const user = createUser('Player Auth');

    await registerPlayer(page, user);
    await page.getByTestId('logout-button').click();
    await expect(page).toHaveURL(/auth\/login/);

    await loginPlayer(page, user);
    await expect(page.getByText(user.displayName)).toBeVisible();
  });

  test('shows an error for invalid credentials', async ({ page }) => {
    await page.goto('auth/login');

    await page.getByTestId('email').fill('missing-user@example.com');
    await page.getByTestId('password').fill('WrongPassword123!');
    await page.getByTestId('login-button').click();

    await expect(page.getByTestId('error-message')).toHaveText('Login failed');
  });
});
