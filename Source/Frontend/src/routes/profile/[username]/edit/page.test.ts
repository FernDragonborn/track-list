// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, cleanup } from '@testing-library/svelte';
import '@testing-library/jest-dom/vitest';

vi.mock('$app/forms', () => ({ enhance: () => ({ destroy: () => {} }) }));
vi.mock('$app/paths', () => ({ resolve: (p: string) => p }));
vi.mock('$env/dynamic/public', () => ({ env: { PUBLIC_API_URL: 'http://localhost/api' } }));

import Page from './+page.svelte';
import type { ProfileDto } from '$lib/types/profileTypes';

function makeProfile(overrides: Partial<ProfileDto> = {}): ProfileDto {
	return {
		username: 'alice',
		displayName: 'Alice Doe',
		bio: 'I love movies.',
		profilePicUrl: 'https://example.com/avatar.jpg',
		followersCount: 5,
		followingCount: 3,
		isFollowing: false,
		...overrides,
	};
}

describe('profile/[username]/edit page', () => {
	beforeEach(() => {
		cleanup();
	});

	it('renders pre-filled form with profile data', () => {
		render(Page, { props: { data: { profile: makeProfile() }, form: null } });
		expect(screen.getByLabelText("Ім'я")).toHaveValue('Alice Doe');
		expect(screen.getByLabelText('Біо')).toHaveValue('I love movies.');
		expect(screen.getByLabelText('Посилання на зображення')).toHaveValue('https://example.com/avatar.jpg');
	});

	it('shows validation error when displayName is empty on blur', async () => {
		render(Page, { props: { data: { profile: makeProfile({ displayName: '' }) }, form: null } });
		const input = screen.getByLabelText("Ім'я");
		await fireEvent.blur(input);
		expect(screen.getByText(/обов.язковим/i)).toBeInTheDocument();
	});

	it('avatar preview updates when URL field changes', async () => {
		render(Page, { props: { data: { profile: makeProfile({ profilePicUrl: '' }) }, form: null } });
		const urlInput = screen.getByLabelText('Посилання на зображення');
		await fireEvent.input(urlInput, { target: { value: 'https://new.example.com/img.jpg' } });
		const img = screen.getByRole('img', { name: 'Аватар' });
		expect(img).toHaveAttribute('src', 'https://new.example.com/img.jpg');
	});

	it('"Скасувати" links back to the profile page', () => {
		render(Page, { props: { data: { profile: makeProfile({ username: 'alice' }) }, form: null } });
		expect(screen.getByRole('link', { name: /Скасувати/i })).toHaveAttribute(
			'href',
			'/profile/alice',
		);
	});
});
