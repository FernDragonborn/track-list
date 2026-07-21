<script lang="ts">
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { lastCrumbLabel, crumbLabels, previousPath } from '$lib/stores/breadcrumb';
	import { truncate } from '$lib/utils/text';

	// Routes that, when navigated FROM, should add a context crumb on the next page.
	// E.g. catalog → media: show "Головна / Каталог / {Title}".
	const CONTEXT_FROM: Record<string, { label: string; href: string }> = {
		'/catalog': { label: 'Каталог', href: '/catalog' },
		'/': { label: 'Стрічка', href: '/' },
	};

	const LABELS: Record<string, string> = {
		profile: 'Профіль',
		collections: 'Добірки',
		catalog: 'Каталог',
		feed: 'Стрічка',
		tracking: 'Трекінг',
		reviews: 'Рецензії',
		moderation: 'Модерація',
		admin: 'Адмін',
		following: 'Підписки',
		auth: 'Авторизація',
		login: 'Вхід',
		register: 'Реєстрація',
		media: 'Медіа',
		settings: 'Налаштування',
	};

	function humanize(seg: string): string {
		if (LABELS[seg]) return LABELS[seg];
		// External media id like "Tmdb:movie:550" — show as "Медіа" placeholder
		// (real title is injected via lastCrumbLabel store by MediaPageView).
		if (/^[A-Za-z]+:[a-z]+:\d+/.test(seg)) return 'Медіа';
		if (seg.length > 24) return truncate(seg, 12, { smart: false });
		return seg;
	}

	// Prefix-only routes that don't have a real index page and would redirect
	// or are just routing conventions (e.g. /profile → own profile, /media/external
	// is just where TMDB-imported items live). Skip them as crumbs.
	const PREFIX_ONLY = new Set(['profile', 'media', 'external']);

	const crumbs = $derived.by(() => {
		const path = page.url.pathname;
		const segs = path.split('/').filter(Boolean);
		const out: { label: string; href: string }[] = [{ label: 'Головна', href: '/' }];

		// Inject "from" context crumb if we arrived from a known navigation source
		// (e.g. came to a media page from /catalog).
		const ctx = $previousPath ? CONTEXT_FROM[$previousPath] : null;
		const isMediaPage = path.startsWith('/media/');
		if (ctx && isMediaPage) {
			out.push(ctx);
		}

		let acc = '';
		for (let i = 0; i < segs.length; i++) {
			const s = segs[i];
			acc += '/' + s;
			// Skip prefix-only segments when not the leaf.
			if (PREFIX_ONLY.has(s) && i < segs.length - 1) continue;
			const isLast = i === segs.length - 1;
			const keyed = $crumbLabels[acc];
			let label = keyed ?? humanize(decodeURIComponent(s));
			if (isLast && $lastCrumbLabel && !keyed) label = $lastCrumbLabel;
			out.push({ label, href: acc });
		}
		return out;
	});
</script>

{#if crumbs.length > 1}
	<nav aria-label="Breadcrumb" class="max-w-7xl mx-auto px-6 py-2 text-sm text-text-muted">
		<ol class="flex flex-wrap items-center gap-1">
			{#each crumbs as crumb, i (crumb.href)}
				<li class="flex items-center gap-1">
					{#if i < crumbs.length - 1}
						<a
							href={resolve(crumb.href as '/')}
							class="hover:text-white/95 transition-colors"
						>
							{crumb.label}
						</a>
						<span class="text-gray-600">/</span>
					{:else}
						<span class="text-white/80 font-medium" aria-current="page">{crumb.label}</span>
					{/if}
				</li>
			{/each}
		</ol>
	</nav>
{/if}
