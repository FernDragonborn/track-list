<script lang="ts">
	interface Props {
		value: number; // 0–10
		interactive?: boolean;
		size?: 'sm' | 'md' | 'lg';
		onchange?: (v: number) => void;
		class?: string;
	}

	let { value, interactive = false, size = 'md', onchange, class: className = '' }: Props = $props();

	let hoverValue = $state(0);

	const effective = $derived(hoverValue || value);

	const starClass = $derived(
		size === 'sm' ? 'text-sm' : size === 'lg' ? 'text-3xl' : 'text-xl',
	);

	// Tailwind yellow-400 / gray-600
	const YELLOW = 'rgb(250 204 21)';
	const GRAY = 'rgb(75 85 99)';

	function styleOf(idx: number): string {
		const full = Math.floor(effective / 2);
		const half = effective % 2 === 1;
		if (idx < full) return `color:${YELLOW}`;
		if (idx === full && half)
			return [
				`background:linear-gradient(to right,${YELLOW} 50%,${GRAY} 50%)`,
				'-webkit-background-clip:text',
				'-webkit-text-fill-color:transparent',
				'background-clip:text',
			].join(';');
		return `color:${GRAY}`;
	}
</script>

<div
	class="flex items-center gap-0.5 {className}"
	onmouseleave={() => { if (interactive) hoverValue = 0; }}
	role="group"
	aria-label="Рейтинг {value}/10"
>
	{#each [0, 1, 2, 3, 4] as idx (idx)}
		<span class="relative inline-block leading-none select-none {starClass}">
			<span style={styleOf(idx)}>★</span>

			{#if interactive}
				<button
					type="button"
					class="absolute inset-y-0 left-0 w-1/2 cursor-pointer"
					onmouseenter={() => (hoverValue = idx * 2 + 1)}
					onclick={() => onchange?.(idx * 2 + 1)}
					aria-label="Оцінка {idx * 2 + 1}"
				></button>
				<button
					type="button"
					class="absolute inset-y-0 right-0 w-1/2 cursor-pointer"
					onmouseenter={() => (hoverValue = idx * 2 + 2)}
					onclick={() => onchange?.(idx * 2 + 2)}
					aria-label="Оцінка {idx * 2 + 2}"
				></button>
			{/if}
		</span>
	{/each}
</div>
