<script lang="ts">
	import { resolve } from '$app/paths';
</script>

<svelte:head>
	<title>Конфіденційність · TrackList</title>
</svelte:head>

<article class="max-w-3xl mx-auto py-8 text-white/85 space-y-6">
	<h1 class="text-3xl font-black text-white/95">Конфіденційність</h1>

	<p class="leading-relaxed">
		Коротко: збираємо мінімум, шифруємо паролі, не продаємо нікому, GDPR-сумісні. Нижче в деталях.
	</p>

	<section class="space-y-3">
		<h2 class="text-xl font-bold text-white/95 border-b border-gray-700/50 pb-2">Що ми зберігаємо</h2>
		<ul class="list-disc list-outside ml-5 space-y-2">
			<li><strong class="text-white/95">Обліковий запис:</strong> email, username, hash пароля (BCrypt + сіль). Пароль у відкритому вигляді не зберігається ніде — навіть адмін бази даних не може його прочитати.</li>
			<li><strong class="text-white/95">Профіль:</strong> ім'я, опис, країна, стать, посилання на аватар. Усе видно публічно — це твій вибір що туди писати.</li>
			<li><strong class="text-white/95">Контент:</strong> твої рецензії, коментарі, оцінки, списки, статуси трекінгу, підписки. Публічно видно (приватні списки — лише тобі).</li>
			<li><strong class="text-white/95">Аватарка:</strong> або URL на DiceBear (зовнішній сервіс), або файл на нашому диску. Не діляться між сервісами.</li>
		</ul>
	</section>

	<section class="space-y-3">
		<h2 class="text-xl font-bold text-white/95 border-b border-gray-700/50 pb-2">Що ми НЕ зберігаємо</h2>
		<ul class="list-disc list-outside ml-5 space-y-1.5">
			<li>IP-адресу (за межами стандартних веб-логів, які ротуються щодня)</li>
			<li>Куки трекінгу — у нас взагалі немає аналітичних куків</li>
			<li>Дані про твою поведінку: які сторінки скільки скролив, де клікав</li>
			<li>Платіжні дані — нема платежів</li>
		</ul>
	</section>

	<section class="space-y-3">
		<h2 class="text-xl font-bold text-white/95 border-b border-gray-700/50 pb-2">Як ми зберігаємо</h2>
		<ul class="list-disc list-outside ml-5 space-y-2">
			<li><strong class="text-white/95">База даних</strong> — SQLite-файл у внутрішньому Docker volume. Не торкається мережі взагалі, лише через наш API (нема навіть TCP-порту, який можна було б attack-нути).</li>
			<li><strong class="text-white/95">Паролі</strong> — BCrypt-hash із індивідуальною сіллю для кожного юзера. Якщо хтось завантажить дамп бази — пароль все одно не відновити.</li>
			<li><strong class="text-white/95">JWT-токени</strong> — підписані секретом, який сидить лише в нашому .env. Зберігаються у твоєму браузері (httpOnly cookie), не пишемо їх у логи.</li>
			<li><strong class="text-white/95">Аватарки</strong> — у Docker volume, доступні лише через Caddy (reverse-proxy). Завантаження валідуються (тільки JPG/PNG, SSRF-захист на зовнішні URL).</li>
			<li><strong class="text-white/95">User-input HTML</strong> — пропускаємо через DOMPurify перед рендером. XSS-вектори вирізаються.</li>
		</ul>
	</section>

	<section class="space-y-3">
		<h2 class="text-xl font-bold text-white/95 border-b border-gray-700/50 pb-2">Чому даним складно витекти</h2>
		<ul class="list-disc list-outside ml-5 space-y-1.5">
			<li>База — SQLite-файл, без жодного мережевого порту назовні</li>
			<li>Усі API виклики йдуть через JWT-перевірку; без токена — 401</li>
			<li>Адмін-ендпойнти захищені окремим policy (Role check)</li>
			<li>Soft-delete: видалене не зникає миттєво, але приховане від запитів через EF query filter</li>
			<li>Жодних third-party скриптів на сторінках (нема Google Analytics, Sentry, Hotjar тощо)</li>
			<li>Зовнішні API (TMDB / OMDb / DeepL / Wikipedia / Letterboxd) отримують лише публічні ідентифікатори: TMDB id, заголовок фільму. Твоїх персональних даних туди не передаємо.</li>
		</ul>
	</section>

	<section class="space-y-3">
		<h2 class="text-xl font-bold text-white/95 border-b border-gray-700/50 pb-2">Твої права (GDPR)</h2>
		<ul class="list-disc list-outside ml-5 space-y-2">
			<li><strong class="text-white/95">Доступ</strong> — усе, що ми про тебе зберігаємо, видно у твоєму профілі та налаштуваннях. Якщо потрібен повний експорт — напиши нам.</li>
			<li><strong class="text-white/95">Виправлення</strong> — редагуєш профіль самостійно на сторінці налаштувань.</li>
			<li><strong class="text-white/95">Видалення (right to be forgotten)</strong> — кнопка видалення акаунта в налаштуваннях, або напиши нам. Видалимо протягом 30 днів. Soft-delete лишає внутрішні ID для referential integrity (як ghost row), але PII (email, username, аватар, тексти рецензій) перетираються.</li>
			<li><strong class="text-white/95">Перенос даних</strong> — можемо віддати твої рецензії й добірки у JSON, якщо попросиш.</li>
			<li><strong class="text-white/95">Відмова від обробки</strong> — якщо щось із цього некомфортно, не реєструйся. Гостьовий доступ до публічного контенту не потребує облікового запису.</li>
		</ul>
	</section>

	<section class="space-y-3">
		<h2 class="text-xl font-bold text-white/95 border-b border-gray-700/50 pb-2">Скільки часу зберігаємо</h2>
		<p class="leading-relaxed">
			Поки існує акаунт — постійно. Після видалення акаунта — soft-delete-row живе ~30 днів (на випадок випадкового видалення), потім hard-delete із бекапів. Логи доступу ротуються щодня з retention 30 днів.
		</p>
	</section>

	<section class="space-y-3">
		<h2 class="text-xl font-bold text-white/95 border-b border-gray-700/50 pb-2">З ким ділимось</h2>
		<p class="leading-relaxed">
			Ні з ким. Ми не передаємо твої дані ні рекламодавцям, ні аналітичним сервісам, ні державним органам (бо нас і не питали — це навчальний проєкт). Якщо коли-небудь дійде до правомірного судового запиту — повідомимо тобі першому, наскільки це дозволено законом.
		</p>
	</section>

	<p class="text-text-muted text-sm pt-2 border-t border-gray-700/50">
		Питання? Пиши на сторінці
		<a href={resolve('/contact')} class="text-brand-accent hover:underline">Контакти</a>.
	</p>
</article>
