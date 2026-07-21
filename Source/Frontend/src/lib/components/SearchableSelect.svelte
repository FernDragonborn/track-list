<script lang="ts">
	interface Option {
		value: string;
		label: string;
	}

	interface Props {
		options: Option[];
		value: string;
		name?: string;
		id?: string;
		placeholder?: string;
	}

	let { options, value = $bindable(), name, id, placeholder = '— Оберіть —' }: Props = $props();

	let open = $state(false);
	let query = $state('');
	let highlightIndex = $state(0);
	let containerEl: HTMLDivElement | undefined = $state();
	let listEl: HTMLUListElement | undefined = $state();

	const filtered = $derived(
		query
			? options.filter((o) => o.label.toLowerCase().includes(query.toLowerCase()))
			: options,
	);

	const selectedLabel = $derived(options.find((o) => o.value === value)?.label ?? placeholder);

	function toggle() {
		open = !open;
		if (open) {
			query = '';
			highlightIndex = 0;
		}
	}

	function select(opt: Option) {
		value = opt.value;
		open = false;
		query = '';
	}

	function clear() {
		value = '';
		open = false;
		query = '';
	}

	function onKeydown(e: KeyboardEvent) {
		if (!open) {
			if (e.key === 'Enter' || e.key === ' ' || e.key === 'ArrowDown') {
				e.preventDefault();
				open = true;
				query = '';
				highlightIndex = 0;
			}
			return;
		}

		switch (e.key) {
			case 'ArrowDown':
				e.preventDefault();
				highlightIndex = Math.min(highlightIndex + 1, filtered.length - 1);
				scrollToHighlighted();
				break;
			case 'ArrowUp':
				e.preventDefault();
				highlightIndex = Math.max(highlightIndex - 1, 0);
				scrollToHighlighted();
				break;
			case 'Enter':
				e.preventDefault();
				if (filtered[highlightIndex]) select(filtered[highlightIndex]);
				break;
			case 'Escape':
				e.preventDefault();
				open = false;
				query = '';
				break;
			case 'Backspace':
				query = query.slice(0, -1);
				highlightIndex = 0;
				break;
			default:
				if (e.key.length === 1 && !e.ctrlKey && !e.metaKey) {
					query += e.key;
					highlightIndex = 0;
				}
		}
	}

	function scrollToHighlighted() {
		requestAnimationFrame(() => {
			const item = listEl?.children[highlightIndex] as HTMLElement | undefined;
			item?.scrollIntoView({ block: 'nearest' });
		});
	}

	function onClickOutside(e: MouseEvent) {
		if (containerEl && !containerEl.contains(e.target as Node)) {
			open = false;
			query = '';
		}
	}
</script>

<svelte:window onclick={onClickOutside} />

<!-- Hidden input for form submission -->
{#if name}
	<input type="hidden" {name} value={value} />
{/if}

<div class="relative" bind:this={containerEl}>
	<button
		type="button"
		{id}
		onclick={toggle}
		onkeydown={onKeydown}
		class="w-full bg-bkg-main text-white/95 px-4 py-3 rounded-lg border border-gray-700
		       focus:border-brand-accent focus:ring-1 focus:ring-brand-accent outline-none
		       transition-all text-left flex items-center justify-between gap-3"
	>
		<span class="truncate {value ? '' : 'text-gray-500'}">{selectedLabel}</span>
		<svg class="w-4 h-4 text-gray-400 shrink-0 transition-transform {open ? 'rotate-180' : ''}"
			fill="none" stroke="currentColor" viewBox="0 0 24 24">
			<path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
		</svg>
	</button>

	{#if open}
		<div class="absolute z-50 mt-1 w-full bg-bkg-header border border-gray-700 rounded-lg shadow-2xl overflow-hidden">
			{#if query}
				<div class="px-3 py-2 text-xs text-text-muted border-b border-gray-700 flex items-center gap-1">
					<span>Пошук:</span>
					<span class="text-brand-accent font-medium">{query}</span>
				</div>
			{/if}

			<ul bind:this={listEl} class="max-h-52 overflow-y-auto">
				{#if placeholder}
					<li>
						<button
							type="button"
							onclick={clear}
							class="w-full text-left px-4 py-2.5 text-sm text-gray-500 hover:bg-white/5 transition-colors"
						>
							{placeholder}
						</button>
					</li>
				{/if}
				{#each filtered as opt, i (opt.value)}
					{@const active = opt.value === value}
					<li>
						<button
							type="button"
							onclick={() => select(opt)}
							class="w-full text-left px-4 py-2.5 text-sm transition-colors
							       {i === highlightIndex ? 'bg-brand-accent/20 text-white/95' : active ? 'text-brand-accent' : 'text-white/90 hover:bg-white/5'}"
						>
							{opt.label}
						</button>
					</li>
				{/each}
				{#if filtered.length === 0}
					<li class="px-4 py-3 text-sm text-text-muted text-center">Нічого не знайдено</li>
				{/if}
			</ul>
		</div>
	{/if}
</div>
