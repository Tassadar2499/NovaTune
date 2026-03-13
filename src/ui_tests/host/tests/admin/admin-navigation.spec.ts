import { expect, test } from '@playwright/test';
import { loginAdmin } from '../support/auth.js';

test.describe('Admin dashboard navigation', () => {
  test('shows overview cards and navigates across admin sections', async ({ page }) => {
    await loginAdmin(page);

    await expect(page.getByTestId('stat-card-totalUsers')).toBeVisible();
    await expect(page.getByTestId('stat-card-totalTracks')).toBeVisible();
    await expect(page.getByTestId('stat-card-totalPlays')).toBeVisible();
    await expect(page.getByTestId('stat-card-activeUsers24h')).toBeVisible();

    await page.getByTestId('nav-users').click();
    await expect(page.getByTestId('users-heading')).toBeVisible();

    await page.getByTestId('nav-tracks').click();
    await expect(page.getByTestId('tracks-heading')).toBeVisible();

    await page.getByTestId('nav-analytics').click();
    await expect(page.getByTestId('analytics-heading')).toBeVisible();

    await page.getByTestId('nav-audit-logs').click();
    await expect(page.getByTestId('audit-logs-heading')).toBeVisible();
    await expect(page.getByTestId('verify-integrity-button')).toBeVisible();
  });
});
