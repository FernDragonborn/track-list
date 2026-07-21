import { redirect, error, fail } from '@sveltejs/kit';
import type { PageServerLoad, Actions } from './$types';
import { api } from '$lib/api';

export const load: PageServerLoad = async ({ params, locals, cookies }) => {
	if (!locals.user) redirect(302, '/auth/login');

	const token = cookies.get('accessToken');

	let collection;
	try {
		collection = await api.getCollectionDetail(params.id, token);
	} catch {
		error(404, 'Добірку не знайдено');
	}

	if (collection.ownerId !== (locals.user.id as string)) {
		throw redirect(302, `/collections/${params.id}`);
	}

	return { collection, token: token ?? null };
};

export const actions: Actions = {
	save: async ({ request, params, cookies, locals }) => {
		if (!locals.user) return fail(401, { message: 'Unauthorized' });
		const token = cookies.get('accessToken');
		const fd = await request.formData();
		const name = (fd.get('name') as string)?.trim();
		const description = (fd.get('description') as string)?.trim() || undefined;
		const privacyLevel = (fd.get('privacyLevel') as 'public' | 'private') || 'public';

		if (!name) return fail(400, { message: 'Назва не може бути порожньою' });

		try {
			await api.updateCollection(params.id, { name, description, privacyLevel }, token);
		} catch (e: unknown) {
			const msg = (e as { body?: { message?: string } })?.body?.message ?? 'Помилка збереження';
			return fail(500, { message: msg });
		}

		return { success: true };
	},

	delete: async ({ params, cookies, locals }) => {
		if (!locals.user) return fail(401, { message: 'Unauthorized' });
		const token = cookies.get('accessToken');

		try {
			await api.deleteCollection(params.id, token);
		} catch (e: unknown) {
			const msg = (e as { body?: { message?: string } })?.body?.message ?? 'Помилка видалення';
			return fail(500, { message: msg });
		}

		throw redirect(302, '/collections');
	},

	grantAccess: async ({ request, params, cookies, locals }) => {
		if (!locals.user) return fail(401, { message: 'Unauthorized' });
		const token = cookies.get('accessToken');
		const fd = await request.formData();
		const username = (fd.get('username') as string)?.trim();

		if (!username) return fail(400, { accessMessage: 'Введіть імʼя користувача' });

		let userId: string;
		try {
			const profile = await api.getProfile(username, token);
			if (!profile.id) return fail(400, { accessMessage: 'Користувача не знайдено' });
			userId = profile.id;
		} catch {
			return fail(400, { accessMessage: `Користувача @${username} не знайдено` });
		}

		try {
			await api.grantCollectionAccess(params.id, userId, token);
		} catch (e: unknown) {
			const msg = (e as { body?: { message?: string } })?.body?.message ?? 'Помилка надання доступу';
			return fail(500, { accessMessage: msg });
		}

		throw redirect(302, `/collections/${params.id}/settings`);
	},

	revokeAccess: async ({ request, params, cookies, locals }) => {
		if (!locals.user) return fail(401, { message: 'Unauthorized' });
		const token = cookies.get('accessToken');
		const fd = await request.formData();
		const targetUserId = fd.get('targetUserId') as string;

		try {
			await api.revokeCollectionAccess(params.id, targetUserId, token);
		} catch (e: unknown) {
			const msg = (e as { body?: { message?: string } })?.body?.message ?? 'Помилка відкликання доступу';
			return fail(500, { message: msg });
		}

		throw redirect(302, `/collections/${params.id}/settings`);
	},
};
