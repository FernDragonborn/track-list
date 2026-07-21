<svelte:head>
	<title>Контакти · TrackList</title>
</svelte:head>

<article class="max-w-3xl mx-auto py-8 text-white/85 space-y-6">
	<h1 class="text-3xl font-black text-white/95">Контакти</h1>

	<p class="leading-relaxed">
		Команда — троє людей. Пиши тому, хто за відповідну частину відповідає, або в спільну скриньку.
	</p>

	<div class="grid gap-4 md:grid-cols-3 my-4">
		<div class="rounded-xl border border-gray-700/50 bg-bkg-header p-5">
			<p class="text-[10px] uppercase tracking-wider text-text-muted mb-1">Загальні питання</p>
			<h3 class="text-base font-bold text-white/95 mb-1">[Ім'я]</h3>
			<p class="text-sm text-text-muted mb-2">Tech lead</p>
			<a href="mailto:placeholder@example.com" class="text-brand-accent text-sm hover:underline break-all">placeholder@example.com</a>
		</div>

		<div class="rounded-xl border border-gray-700/50 bg-bkg-header p-5">
			<p class="text-[10px] uppercase tracking-wider text-text-muted mb-1">Frontend</p>
			<h3 class="text-base font-bold text-white/95 mb-1">[Ім'я]</h3>
			<p class="text-sm text-text-muted mb-2">SvelteKit, Tailwind, UX</p>
			<a href="mailto:placeholder@example.com" class="text-brand-accent text-sm hover:underline break-all">placeholder@example.com</a>
		</div>

		<div class="rounded-xl border border-gray-700/50 bg-bkg-header p-5">
			<p class="text-[10px] uppercase tracking-wider text-text-muted mb-1">Backend</p>
			<h3 class="text-base font-bold text-white/95 mb-1">[Ім'я]</h3>
			<p class="text-sm text-text-muted mb-2">ASP.NET, EF Core, інтеграції</p>
			<a href="mailto:placeholder@example.com" class="text-brand-accent text-sm hover:underline break-all">placeholder@example.com</a>
		</div>
	</div>

	<section class="space-y-3">
		<h2 class="text-xl font-bold text-white/95 border-b border-gray-700/50 pb-2">Інше</h2>
		<ul class="list-disc list-outside ml-5 space-y-2">
			<li><strong class="text-white/95">Баги, фіча-реквести</strong> — issues на <a href="https://github.com/FernDragonborn/track-list/issues" target="_blank" rel="noopener noreferrer" class="text-brand-accent hover:underline">GitHub</a></li>
			<li><strong class="text-white/95">Security disclosure</strong> — пиши приватно, не відкривай публічного issue. У відповіді — впродовж 7 днів.</li>
			<li><strong class="text-white/95">GDPR-запити</strong> (видалення / експорт даних) — будь-яка з email-адрес вище. Відповідь — впродовж 30 днів.</li>
		</ul>
	</section>

	<p class="text-text-muted text-sm pt-2 border-t border-gray-700/50">
		Це навчальний дипломний проєкт. Production-підтримки немає, гарантій теж.
	</p>
</article>
