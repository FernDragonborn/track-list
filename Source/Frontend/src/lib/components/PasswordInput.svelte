<script lang="ts">
	interface Props {
		id: string;
		name: string;
		value: string;
		placeholder?: string;
		required?: boolean;
		className?: string;
		ariaInvalid?: boolean;
		onblur?: () => void;
		oninput?: () => void;
	}

	let {
		id,
		name,
		value = $bindable(),
		placeholder = '••••••••',
		required = false,
		className = '',
		ariaInvalid = false,
		onblur,
		oninput,
	}: Props = $props();

	let visible = $state(false);
</script>

<div class="relative">
	<input
		{id}
		{name}
		type={visible ? 'text' : 'password'}
		bind:value
		{placeholder}
		{required}
		{onblur}
		{oninput}
		aria-invalid={ariaInvalid}
		class="{className} pr-12"
	/>
	<button
		type="button"
		onclick={() => (visible = !visible)}
		aria-label={visible ? 'Сховати пароль' : 'Показати пароль'}
		tabindex="-1"
		class="absolute right-3 top-1/2 -translate-y-1/2 p-1 text-text-muted hover:text-white/95 transition-colors"
	>
		{#if visible}
			<svg
				xmlns="http://www.w3.org/2000/svg"
				width="20"
				height="20"
				viewBox="0 0 24 24"
				fill="none"
				stroke="currentColor"
				stroke-width="2"
				stroke-linecap="round"
				stroke-linejoin="round"
			>
				<path
					d="M9.88 9.88a3 3 0 1 0 4.24 4.24M10.73 5.08A10.43 10.43 0 0 1 12 5c7 0 10 7 10 7a13.16 13.16 0 0 1-1.67 2.68M6.61 6.61A13.526 13.526 0 0 0 2 12s3 7 10 7a9.74 9.74 0 0 0 5.39-1.61M2 2l20 20"
				/>
			</svg>
		{:else}
			<svg
				xmlns="http://www.w3.org/2000/svg"
				width="20"
				height="20"
				viewBox="0 0 24 24"
				fill="none"
				stroke="currentColor"
				stroke-width="2"
				stroke-linecap="round"
				stroke-linejoin="round"
			>
				<path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z" />
				<circle cx="12" cy="12" r="3" />
			</svg>
		{/if}
	</button>
</div>
