<script lang="ts" generics="T extends string | number">
	interface Option {
		value: T;
		label: string;
	}

	interface Props {
		options: Option[];
		selected: T[];
		placeholder?: string;
		disabled?: boolean;
	}

	let {
		options,
		selected = $bindable<T[]>([]),
		placeholder = 'Обрати',
		disabled = false,
	}: Props = $props();

	let open = $state(false);
	let containerEl = $state<HTMLDivElement | undefined>();

	const buttonLabel = $derived(
		selected.length === 0
			? placeholder
			: `${placeholder}: ${selected.length}`,
	);

	function toggle(value: T) {
		if (selected.includes(value)) {
			selected = selected.filter((v) => v !== value);
		} else {
			selected = [...selected, value];
		}
	}

	function clearAll(e: Event) {
		e.stopPropagation();
		selected = [];
	}

	function onDocClick(e: MouseEvent) {
		if (!containerEl) return;
		if (!containerEl.contains(e.target as Node)) open = false;
	}

	function onKey(e: KeyboardEvent) {
		if (e.key === 'Escape') open = false;
	}

	$effect(() => {
		if (open) {
			document.addEventListener('mousedown', onDocClick);
			document.addEventListener('keydown', onKey);
			return () => {
				document.removeEventListener('mousedown', onDocClick);
				document.removeEventListener('keydown', onKey);
			};
		}
	});
</script>

<div bind:this={containerEl} class="relative inline-block">
	<button
		type="button"
		{disabled}
		onclick={() => (open = !open)}
		class="bg-bkg-header text-white/95 text-sm px-3 py-1.5 rounded-md border border-gray-700/50 focus:border-brand-accent outline-none inline-flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed min-w-[180px]"
	>
		<span class="flex-1 text-left">{buttonLabel}</span>
		{#if selected.length > 0}
			<span
				role="button"
				tabindex="0"
				onclick={clearAll}
				onkeydown={(e) => e.key === 'Enter' && clearAll(e)}
				class="text-text-muted hover:text-white"
				title="Очистити"
			>
				✕
			</span>
		{/if}
		<svg width="10" height="10" viewBox="0 0 10 10" fill="currentColor">
			<path d="M5 7L1 3h8L5 7z" />
		</svg>
	</button>

	{#if open}
		<div
			class="absolute top-full left-0 mt-1 w-64 max-h-72 overflow-y-auto bg-bkg-header border border-gray-700 rounded-md shadow-xl z-50"
		>
			{#if options.length === 0}
				<div class="px-3 py-2 text-sm text-text-muted">Немає опцій</div>
			{:else}
				{#each options as opt (opt.value)}
					<label
						class="flex items-center gap-2 px-3 py-2 cursor-pointer hover:bg-white/5 text-sm text-white/90"
					>
						<input
							type="checkbox"
							checked={selected.includes(opt.value)}
							onchange={() => toggle(opt.value)}
							class="accent-brand-accent"
						/>
						{opt.label}
					</label>
				{/each}
			{/if}
		</div>
	{/if}
</div>
