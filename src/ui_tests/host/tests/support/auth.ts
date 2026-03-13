import { expect, type Page } from '@playwright/test';
import { adminCredentials, type Credentials } from './runtime.js';

export async function registerPlayer(page: Page, user: Credentials): Promise<void> {
  await page.goto('auth/register');

  await page.getByTestId('display-name').fill(user.displayName);
  await page.getByTestId('email').fill(user.email);
  await page.getByTestId('password').fill(user.password);
  await page.getByTestId('confirm-password').fill(user.password);
  await page.getByTestId('register-button').click();

  await expect(page.getByTestId('library-heading')).toBeVisible();
}

export async function loginPlayer(page: Page, user: Credentials): Promise<void> {
  await page.goto('auth/login');

  await page.getByTestId('email').fill(user.email);
  await page.getByTestId('password').fill(user.password);
  await page.getByTestId('login-button').click();

  await expect(page.getByTestId('library-heading')).toBeVisible();
}

export async function loginAdmin(page: Page): Promise<void> {
  await page.goto('auth/login');

  await page.getByTestId('email').fill(adminCredentials.email);
  await page.getByTestId('password').fill(adminCredentials.password);
  await page.getByTestId('login-button').click();

  await expect(page.getByTestId('dashboard-heading')).toBeVisible();
}
