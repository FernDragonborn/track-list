<script lang="ts">
	import type { PageData } from './$types';
	let { data }: { data: PageData } = $props();

	function fmt(n: number | undefined | null): string {
		if (n === null || n === undefined) return '—';
		return n.toLocaleString('uk-UA');
	}
</script>

<svelte:head>
	<title>Про нас · TrackList</title>
</svelte:head>

<article class="max-w-3xl mx-auto py-8 text-white/85 space-y-6">
	<h1 class="text-3xl font-black text-white/95">Про нас</h1>

	<p class="leading-relaxed">
		TrackList — навчальний дипломний проєкт. Сайт для трекінгу та обговорення фільмів і серіалів:
		дивишся, ставиш оцінку, пишеш рецензію, бачиш, що дивляться інші. Без алгоритмів, які
		підсовують контент, без реклами, без аналітики поведінки.
	</p>

	{#if data.stats}
		<section class="space-y-3">
			<h2 class="text-xl font-bold text-white/95 border-b border-gray-700/50 pb-2">У числах</h2>
			<div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
				<div class="rounded-xl border border-gray-700/50 bg-bkg-header p-4 text-center">
					<p class="text-2xl font-black text-brand-accent tabular-nums">{fmt(data.stats.users)}</p>
					<p class="text-[11px] uppercase tracking-wider text-text-muted mt-1">Користувачів</p>
				</div>
				<div class="rounded-xl border border-gray-700/50 bg-bkg-header p-4 text-center">
					<p class="text-2xl font-black text-brand-accent tabular-nums">{fmt(data.stats.media)}</p>
					<p class="text-[11px] uppercase tracking-wider text-text-muted mt-1">Медіа</p>
					<p class="text-[10px] text-text-muted mt-0.5">{fmt(data.stats.movies)} ф · {fmt(data.stats.series)} с</p>
				</div>
				<div class="rounded-xl border border-gray-700/50 bg-bkg-header p-4 text-center">
					<p class="text-2xl font-black text-brand-accent tabular-nums">{fmt(data.stats.reviews)}</p>
					<p class="text-[11px] uppercase tracking-wider text-text-muted mt-1">Рецензій</p>
					<p class="text-[10px] text-text-muted mt-0.5">{fmt(data.stats.reviewsWithText)} з текстом</p>
				</div>
				<div class="rounded-xl border border-gray-700/50 bg-bkg-header p-4 text-center">
					<p class="text-2xl font-black text-brand-accent tabular-nums">
						{data.stats.avgRating !== null ? data.stats.avgRating.toFixed(1) : '—'}
					</p>
					<p class="text-[11px] uppercase tracking-wider text-text-muted mt-1">Середня оцінка</p>
					<p class="text-[10px] text-text-muted mt-0.5">з 10</p>
				</div>
			</div>
			<p class="text-[10px] text-text-muted">
				Кешується ~10 хв, оновлено: {new Date(data.stats.computedAt).toLocaleString('uk-UA')}
			</p>
		</section>
	{/if}

	<section class="space-y-3">
		<h2 class="text-xl font-bold text-white/95 border-b border-gray-700/50 pb-2">Що тут є</h2>
		<ul class="list-disc list-outside ml-5 space-y-1.5">
			<li>Каталог фільмів і серіалів через TMDB</li>
			<li>Власні рецензії з оцінкою 1–10, лайки та коментарі (markdown працює)</li>
			<li>Стрічка від тих, на кого ти підписаний, або глобальна</li>
			<li>Списки/добірки (публічні та приватні)</li>
			<li>Зовнішні дані: рейтинги IMDb / Rotten Tomatoes / Metacritic, "Critical reception" із Wikipedia, рецензії з відомих Letterboxd-юзерів</li>
			<li>Переклад зовнішнього контенту через DeepL (UA / EN перемикач у хедері)</li>
		</ul>
	</section>

	<section class="space-y-3">
		<h2 class="text-xl font-bold text-white/95 border-b border-gray-700/50 pb-2">Чого тут немає</h2>
		<ul class="list-disc list-outside ml-5 space-y-1.5">
			<li>Реклами і трекінг-скриптів</li>
			<li>Cookie-банерів — бо не використовуємо аналітичні куки</li>
			<li>Платної підписки</li>
		</ul>
	</section>

	<p class="text-text-muted text-sm pt-2 border-t border-gray-700/50">
		Open-source:
		<a href="https://github.com/FernDragonborn/track-list" target="_blank" rel="noopener noreferrer" class="text-brand-accent hover:underline">github.com/FernDragonborn/track-list</a>.
		Знайшов баг — відкрий issue.
	</p>
</article>
