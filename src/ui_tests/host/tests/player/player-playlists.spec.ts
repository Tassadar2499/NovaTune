import { expect, test } from '@playwright/test';
import { registerPlayer } from '../support/auth.js';
import { createUser } from '../support/runtime.js';

test.describe('Player playlists', () => {
  test('creates a playlist from the empty state', async ({ page }) => {
    const playlistName = `Roadtrip ${Date.now()}`;

    await registerPlayer(page, createUser('Player Playlists'));
    await page.getByTestId('nav-playlists').click();

    await expect(page.getByTestId('playlists-heading')).toBeVisible();
    await expect(page.getByTestId('empty-state')).toContainText('No playlists yet');

    await page.getByTestId('create-playlist-button').click();
    await page.getByTestId('playlist-name-input').fill(playlistName);
    await page.getByTestId('playlist-description-input').fill('Created by Playwright');
    await page.getByTestId('playlist-submit-button').click();

    await expect(page.getByTestId('playlist-card')).toContainText(playlistName);
  });
});
