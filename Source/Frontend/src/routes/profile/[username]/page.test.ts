// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, cleanup } from '@testing-library/svelte';
import '@testing-library/jest-dom/vitest';

vi.mock('$app/paths', () => ({ resolve: (p: string) => p }));
vi.mock('$app/navigation', () => ({ goto: vi.fn() }));
vi.mock('$env/dynamic/public', () => ({ env: { PUBLIC_API_URL: 'http://localhost/api' } }));
vi.mock('$lib/api', () => ({ api: { get: vi.fn() } }));

import Page from './+page.svelte';
import type { ProfileDto } from '$lib/types/profileTypes';

function makeProfile(overrides: Partial<ProfileDto> = {}): ProfileDto {
	return {
		username: 'testuser',
		followersCount: 10,
		followingCount: 5,
		isFollowing: false,
		...overrides,
	};
}

describe('profile/[username] page', () => {
	beforeEach(() => {
		cleanup();
	});

	describe('own profile', () => {
		it('shows "Редагувати профіль" button', () => {
			render(Page, { props: { data: { profile: makeProfile(), isOwnProfile: true } } });
			expect(screen.getByRole('link', { name: /Редагувати профіль/i })).toBeInTheDocument();
		});

		it('does not show "Підписатися" button', () => {
			render(Page, { props: { data: { profile: makeProfile(), isOwnProfile: true } } });
			expect(screen.queryByRole('button', { name: /Підписатися/i })).not.toBeInTheDocument();
		});

		it('edit link points to /profile/{username}/edit', () => {
			render(Page, { props: { data: { profile: makeProfile({ username: 'alice' }), isOwnProfile: true } } });
			expect(screen.getByRole('link', { name: /Редагувати профіль/i })).toHaveAttribute(
				'href',
				'/profile/alice/edit',
			);
		});
	});

	describe('other user profile', () => {
		it('shows "Підписатися" button', () => {
			render(Page, { props: { data: { profile: makeProfile(), isOwnProfile: false } } });
			expect(screen.getByRole('button', { name: /Підписатися/i })).toBeInTheDocument();
		});

		it('does not show "Редагувати профіль"', () => {
			render(Page, { props: { data: { profile: makeProfile(), isOwnProfile: false } } });
			expect(screen.queryByRole('link', { name: /Редагувати профіль/i })).not.toBeInTheDocument();
		});
	});

	describe('profile data display', () => {
		it('shows username', () => {
			render(Page, { props: { data: { profile: makeProfile({ username: 'alice' }), isOwnProfile: false } } });
			expect(screen.getByText('alice')).toBeInTheDocument();
		});

		it('shows display name when present', () => {
			render(Page, {
				props: { data: { profile: makeProfile({ displayName: 'Alice Doe' }), isOwnProfile: false } },
			});
			expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Alice Doe');
		});

		it('shows username as heading when no display name', () => {
			render(Page, { props: { data: { profile: makeProfile({ username: 'noname' }), isOwnProfile: false } } });
			expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('noname');
		});

		it('shows followers count', () => {
			render(Page, { props: { data: { profile: makeProfile({ followersCount: 42 }), isOwnProfile: false } } });
			expect(screen.getByText('42')).toBeInTheDocument();
		});

		it('shows following count', () => {
			render(Page, { props: { data: { profile: makeProfile({ followingCount: 7 }), isOwnProfile: false } } });
			expect(screen.getByText('7')).toBeInTheDocument();
		});

		it('shows bio when present', () => {
			render(Page, {
				props: { data: { profile: makeProfile({ bio: 'Love movies.' }), isOwnProfile: false } },
			});
			expect(screen.getByText('Love movies.')).toBeInTheDocument();
		});

		it('shows empty state when bio is absent', () => {
			render(Page, { props: { data: { profile: makeProfile(), isOwnProfile: false } } });
			expect(screen.getByText('Немає інформації')).toBeInTheDocument();
		});

		it('avatar uses profilePicUrl when set', () => {
			render(Page, {
				props: {
					data: {
						profile: makeProfile({ profilePicUrl: '/img/avatar.jpg' }),
						isOwnProfile: false,
					},
				},
			});
			const img = screen.getByRole('img');
			expect(img).toHaveAttribute('src', '/img/avatar.jpg');
		});

		it('avatar falls back to ui-avatars when no profilePicUrl', () => {
			render(Page, {
				props: { data: { profile: makeProfile({ username: 'fallback' }), isOwnProfile: false } },
			});
			const img = screen.getByRole('img');
			expect(img.getAttribute('src')).toContain('ui-avatars.com');
		});
	});
});
