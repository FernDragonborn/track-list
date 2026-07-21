<script lang="ts">
	/* eslint-disable svelte/no-navigation-without-resolve -- Wikipedia source URL is external */
	import { selectedLang } from '$lib/stores/language';
	import { api } from '$lib/api';
	import { truncate } from '$lib/utils/text';
	import type { WikiReception } from '$lib/types/externalTypes';

	interface Props {
		reception: WikiReception | null;
	}
	let { reception }: Props = $props();

	let expanded = $state(false);
	let fullText = $state(false);
	let manualTranslations = $state<Record<string, string>>({});
	let translating = $state(false);
	let showOriginal = $state(false);

	const PREVIEW_CHARS = 400;
	const cachedTranslation = $derived.by(() => {
		if (!reception) return null;
		return reception.translations?.[$selectedLang] ?? manualTranslations[$selectedLang] ?? null;
	});
	const hasTranslation = $derived(!!cachedTranslation);
	const localized = $derived.by(() => {
		if (!reception) return { text: '', translated: false };
		if (cachedTranslation && $selectedLang !== 'en' && !showOriginal)
			return { text: cachedTranslation, translated: true };
		return { text: reception.content, translated: false };
	});
	const canToggle = $derived(!!reception && $selectedLang !== 'en');
	const buttonLabel = $derived.by(() => {
		if (translating) return 'Перекладаю…';
		if (!hasTranslation) return `Перекласти на ${$selectedLang.toUpperCase()}`;
		return showOriginal ? `Показати переклад (${$selectedLang.toUpperCase()})` : 'Показати оригінал';
	});

	async function toggle() {
		if (!reception || !canToggle) return;
		if (hasTranslation) {
			showOriginal = !showOriginal;
			return;
		}
		if (translating) return;
		translating = true;
		try {
			const r = await api.translateExternalReview(reception.id, $selectedLang);
			manualTranslations = { ...manualTranslations, [r.lang]: r.translation };
			showOriginal = false;
		} catch {
			// noop
		} finally {
			translating = false;
		}
	}
	const needsTrim = $derived(localized.text.length > PREVIEW_CHARS);
	const visibleText = $derived(
		!needsTrim || fullText ? localized.text : truncate(localized.text, PREVIEW_CHARS),
	);
</script>

{#if reception}
	<section class="mb-6 rounded-xl border border-gray-700/50 bg-bkg-header/70 overflow-hidden">
		<div class="w-full flex items-center justify-between px-4 py-3 gap-3 hover:bg-bkg-header transition-colors">
			<!-- Title area is the click target for expand/collapse (button-like div, keyboard-accessible). -->
			<div
				role="button"
				tabindex="0"
				onclick={() => (expanded = !expanded)}
				onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); expanded = !expanded; } }}
				class="flex-1 flex items-center gap-2 min-w-0 cursor-pointer text-left"
				aria-expanded={expanded}
			>
				<span class="inline-flex items-center justify-center w-6 h-6 rounded-full bg-gray-800 text-gray-300 font-bold text-xs shrink-0">W</span>
				<span class="text-sm font-semibold text-white/90 truncate">Критичний прийом (Wikipedia)</span>
				<span class="text-[10px] text-text-muted px-1.5 py-0.5 border border-gray-600 rounded-md uppercase shrink-0">external</span>
			</div>
			<button
				type="button"
				onclick={() => (expanded = !expanded)}
				class="text-text-muted text-xs flex items-center gap-2 shrink-0"
				aria-label={expanded ? 'Згорнути' : 'Розгорнути'}
			>
				<span>{expanded ? 'Згорнути' : 'Розгорнути'}</span>
				<svg
					class="w-4 h-4 transition-transform {expanded ? 'rotate-180' : ''}"
					fill="none"
					stroke="currentColor"
					viewBox="0 0 24 24"
				>
					<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
				</svg>
			</button>
		</div>

		{#if expanded}
			<div class="px-4 pb-4 pt-1 border-t border-gray-700/40">
				<div class="flex items-start justify-between gap-3 mb-2 flex-wrap">
					<p class="text-[11px] text-yellow-400/80">
						Можливі спойлери — текст з Wikipedia.
					</p>
					{#if reception && canToggle}
						<button
							type="button"
							onclick={toggle}
							disabled={translating}
							class="text-xs text-brand-accent hover:underline disabled:opacity-50 whitespace-nowrap"
						>{buttonLabel}</button>
					{/if}
				</div>
				{#if reception}
					{#if localized.translated}
						<p class="text-[10px] text-text-muted mb-2 italic">Перекладено DeepL</p>
					{/if}
					<p class="text-white/80 leading-relaxed whitespace-pre-wrap text-sm">
						{visibleText}
					</p>
					{#if needsTrim}
						<button
							type="button"
							onclick={() => (fullText = !fullText)}
							class="mt-2 text-xs text-brand-accent hover:underline"
						>
							{fullText ? 'Згорнути текст' : 'Розгорнути повністю'}
						</button>
					{/if}
					{#if reception.sourceUrl}
						<div class="mt-3 text-xs text-text-muted">
							Джерело:
							<a href={reception.sourceUrl} target="_blank" rel="noopener noreferrer" class="text-brand-accent hover:underline">
								Wikipedia
							</a>
							<span class="text-[10px]"> · CC BY-SA</span>
						</div>
					{/if}
				{/if}
			</div>
		{/if}
	</section>
{/if}
