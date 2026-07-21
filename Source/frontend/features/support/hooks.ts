import { Before, After, BeforeAll } from '@cucumber/cucumber';
import { request } from '@playwright/test';
import { CustomWorld } from './world.js';

// Seed canonical test users once before any scenario runs.
// This ensures "user" exists for auth scenarios that lack an explicit Given step.
BeforeAll({ timeout: 30000 }, async function () {
	try {
		const api = await request.newContext({
			baseURL: 'http://localhost:80/api/',
			timeout: 8000,
		});
		// Seed 'user' with a non-conflicting email so user@example.com stays free for registration tests
		await api.post('debug/ensure-deleted', {
			data: { username: 'user' },
		}).catch(() => {});
		await api.post('debug/ensure-user', {
			data: { username: 'user', password: 'Password123', email: 'user@app.com' },
		}).catch(() => {});
		for (const username of ['other_user', 'existing_user', 'logouttest']) {
			await api.post('debug/ensure-deleted', {
				data: { username },
			}).catch(() => {});
			await api.post('debug/ensure-user', {
				data: { username, password: 'Password123', email: `${username}@app.com` },
			}).catch(() => {});
		}
		await api.post('debug/ensure-deleted', {
			data: { username: 'testuser' },
		}).catch(() => {});
		await api.post('debug/ensure-user', {
			data: { username: 'testuser', password: 'Password123', email: 'testuser@example.com' },
		}).catch(() => {});
		// Delete new_user so registration happy-path scenario can run cleanly each time
		await api.post('debug/ensure-deleted', {
			data: { username: 'new_user' },
		}).catch(() => {});
		await api.dispose();
	} catch {
		console.log('BeforeAll: API unreachable — test users not pre-seeded');
	}
});

Before(async function (this: CustomWorld) {
	await this.openBrowser();
});

After(async function (this: CustomWorld) {
	await this.closeBrowser();
});
