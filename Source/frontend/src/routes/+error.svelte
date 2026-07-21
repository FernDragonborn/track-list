<script lang="ts">
	import { page } from '$app/stores';
	import { resolve } from '$app/paths';
	import errorImage from '$lib/assets/error-image.png';
</script>

<div class="min-h-screen flex flex-col items-center justify-center p-6 bg-[var(--color-bkg-main)]">
	<div class="max-w-md w-full flex flex-col items-center p-8 bg-[var(--color-bkg-header)] rounded-xl border border-white/10">
		
		<img 
			src={errorImage} 
			alt="Error Illustration" 
			class="w-full max-w-[200px] h-auto object-contain mb-6" 
		/>

		<div class="text-[var(--color-brand-accent)] font-black text-5xl mb-4">
			{$page.status}
		</div>

		<h1 class="text-2xl font-bold mb-2 text-[var(--color-text-main)] text-center">
			Упс! Сталася помилка
		</h1>

		<p class="text-[var(--color-text-muted)] mb-8 text-center">
			{$page.error?.message || 'Щось пішло не так.'}
		</p>

		<a 
			href={resolve('/')} 
			class="inline-flex items-center justify-center px-6 py-3 text-white bg-[var(--color-brand-accent)] rounded-md w-full sm:w-auto"
		>
			Повернутися на головну
		</a>
	</div>
</div>
