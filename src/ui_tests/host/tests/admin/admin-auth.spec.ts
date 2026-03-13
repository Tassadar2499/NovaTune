import { expect, test } from '@playwright/test';
import { loginAdmin } from '../support/auth.js';
import { createUser } from '../support/runtime.js';

test.describe('Admin authentication', () => {
  test('rejects a non-admin account at admin login', async ({ page }) => {
    const user = createUser('Admin Access');

    await page.goto('auth/register');
    await page.getByTestId('display-name').fill(user.displayName);
    await page.getByTestId('email').fill(user.email);
    await page.getByTestId('password').fill(user.password);
    await page.getByTestId('confirm-password').fill(user.password);
    await page.getByTestId('register-button').click();

    await expect(page.getByTestId('success-message')).toContainText('Account created successfully');

    await page.getByTestId('email').fill(user.email);
    await page.getByTestId('password').fill(user.password);
    await page.getByTestId('login-button').click();

    await expect(page.getByTestId('error-message')).toHaveText('Admin access required');
  });

  test('allows the seeded admin to sign in', async ({ page }) => {
    await loginAdmin(page);

    await expect(page.getByTestId('dashboard-heading')).toBeVisible();
    await expect(page.getByTestId('nav-dashboard')).toBeVisible();
  });
});
