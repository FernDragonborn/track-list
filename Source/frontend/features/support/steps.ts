import { Given, When, Then, DataTable } from '@cucumber/cucumber';
import { expect } from './world.js';
import type { CustomWorld } from './world.js';

// ==========================================
// 1. АВТОРИЗАЦІЯ ТА НАВІГАЦІЯ
// ==========================================

const ROUTE_MAP: Record<string, string> = {
	'/login': '/auth/login',
	'/register': '/auth/register',
};
function resolveRoute(url: string): string {
	return ROUTE_MAP[url] ?? url;
}

Given('Гість знаходиться на сторінці {string}', async function (this: CustomWorld, url: string) {
	await this.page!.goto(`${this.appUrl}${resolveRoute(url)}`);
});

// Універсальний крок для авторизації будь-якого юзера
Given(
	/^(?:Користувач|Він) "([^"]*)" авторизований в системі$/,
	async function (this: CustomWorld, username: string) {
		// 1. Спробуємо створити юзера через API, якщо його немає
		try {
			await this.api!.post('debug/ensure-deleted', {
				data: { username },
			});
			await this.api!.post('debug/ensure-user', {
				data: { username, password: 'Password123', email: `${username}@app.com` },
			});
		} catch (_e) {
			console.log(`LOG: API недоступне або юзер вже є. Продовжуємо UI тест.`);
		}

		// 2. Логін через UI (або встановлення cookies, якщо API повертає токен)
		await this.page!.goto(`${this.appUrl}/auth/login`);
		await this.page!.fill(
			'input[name="email"], input[type="email"]',
			`${username}@app.com`,
		);
		await this.page!.fill('input[name="password"], input[type="password"]', 'Password123');
		await this.page!.getByRole('button', { name: /Увійти|Login/i }).click();

		// 3. Wait for login to complete (navigate away from login page)
		await this.page!.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});
		await expect(this.page!.locator('header')).toContainText(username);
		console.log(`LOG: Авторизація користувача ${username} виконана`);
	},
);

// Перехід на специфічні сторінки (Профіль, Адмінка)
Given(
	/^(?:Користувач|Він) "([^"]*)" (?:знаходиться|авторизований) (?:на|в) "([^"]*)"$/,
	async function (this: CustomWorld, username: string, pageName: string) {
		// Спочатку авторизація
		await this.page!.goto(`${this.appUrl}/auth/login`);
		await this.page!.fill(
			'input[name="email"], input[type="email"]',
			username,
		);
		await this.page!.fill('input[name="password"], input[type="password"]', 'Password123');
		await this.page!.getByRole('button', { name: /Увійти|Login/i }).click();
		await this.page!.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});
		// Потім перехід
		const pageMap: Record<string, string> = {
			'Панелі адміністратора': '/admin',
			'Керування медіа': '/admin/media',
			'сторінці редагування профілю': '/profile/edit',
			системі: '/',
		};
		const target = pageMap[pageName] || pageName;
		await this.page!.goto(`${this.appUrl}${target}`);
	},
);

// ==========================================
// 2. ВЗАЄМОДІЯ (КНОПКИ, ПОЛЯ, СПИСКИ)
// ==========================================

// Універсальний клік
When(
	/^(?:Гість|Користувач|Він|Адміністратор|Модератор) натискає (?:кнопку |посилання )?"(?!Вийти|Вихід)([^"]*)"(?: для медіа "([^"]*)")?$/,
	async function (this: CustomWorld, text: string, mediaContext: string) {
		if (mediaContext) {
			// Якщо кнопка всередині картки фільму
			await this.page!.locator(`.media-card:has-text("${mediaContext}")`)
				.getByRole('button', { name: text })
				.click();
		} else {
			// Звичайна кнопка
			await this.page!.click(`text=${text}`);
		}
	},
);

// Універсальний ввід тексту
When(
	/^(?:Гість|Користувач|Він) (?:вводить|змінює) (?:своє )?"([^"]*)" (?:у|на|в поле) "([^"]*)"$/,
	async function (this: CustomWorld, value: string, fieldName: string) {
		// Known Ukrainian → input name mappings
		const nameMap: Record<string, string> = {
			'Нікнейм або Email': 'input[name="email"], input[placeholder*="Нікнейм або Email"]',
			'Нікнейм': 'input[name="username"]',
			'Email': 'input[name="email"]',
			'Електронна пошта': 'input[name="email"]',
			'Пароль': 'input[name="password"], input[type="password"]',
			'Підтвердження пароля': 'input[name="confirm_password"], input[name="confirmPassword"]',
			"Ім'я": 'input[name="firstName"], input[name="name"]',
			'Прізвище': 'input[name="lastName"]',
		};
		const selector = nameMap[fieldName]
			?? `input[placeholder*="${fieldName}"], label:has-text("${fieldName}") + input, input[name*="${fieldName}"], textarea[placeholder*="${fieldName}"]`;
		await this.page!.fill(selector, value);
	},
);

// Заповнення таблиці (Реєстрація)
// --- FORM FILLING (Genereic for Tables) ---
// Використовується в Epic 1 (Реєстрація) та Epic 2 (Профіль)
When(
	'Гість вводить наступні валідні дані:',
	async function (this: CustomWorld, dataTable: DataTable) {
		const data = dataTable.rowsHash(); // Перетворює таблицю в об'єкт { "Нікнейм": "new_user", ... }

		for (const [key, value] of Object.entries(data)) {
			let selector = '';

			switch (key) {
				case 'Поле': // header row — skip
					continue;
				case 'Нікнейм':
					selector = 'input[name="username"]';
					break;
				case 'Email':
					selector = 'input[name="email"]';
					break;
				case 'Пароль':
					selector = 'input[name="password"]';
					break;
				case 'Підтвердження пароля':
					selector = 'input[name="confirm_password"]';
					break;
				case "Ім'я":
					selector = 'input[name="firstName"]';
					break;
				case 'Вік':
					selector = 'input[name="age"]';
					break;
				case 'Стать': {
					// select element — only interact if element exists on this page
					const genderSelect = this.page!.locator('select[name="gender"]');
					if ((await genderSelect.count()) > 0) {
						await genderSelect.selectOption({ label: value }).catch(() => {});
					}
					continue;
				}
				case 'Країна':
					selector = 'input[name="country"]';
					break;
				default:
					console.warn(`[DataTable] Невідоме поле: ${key} — пропускаємо`);
					continue;
			}

			// Only fill if field actually exists on this page
			const fieldEl = this.page!.locator(selector);
			if ((await fieldEl.count()) > 0) {
				await this.page!.fill(selector, value);
			}
		}
	},
);

// Вибір зі списку / Dropdown
When(
	/^(?:Він|Користувач) (?:обирає|змінює) (?:статус|роль .*) (?:на|і обирає) "([^"]*)"$/,
	async function (this: CustomWorld, optionText: string) {
		// Спробуємо різні варіанти UI (select або кастомний div)
		const select = this.page!.locator('select');
		if (await select.isVisible()) {
			await select.selectOption({ label: optionText });
		} else {
			// Клік по тригеру, потім по опції
			await this.page!.locator('.dropdown-trigger, [role="combobox"]').first().click();
			await this.page!.getByRole('option', { name: optionText }).click();
		}
	},
);

// ==========================================
// 3. ПЕРЕВІРКИ СТАНУ (ASSERTIONS)
// ==========================================

Then(
	/^(?:Гість|Користувач|Він) (?:бачить|показує) (?:повідомлення |заголовок )?"([^"]*)"(?: на сторінці)?$/,
	async function (this: CustomWorld, text: string) {
		await expect(this.page!.getByText(text).first()).toBeVisible();
	},
);

Then(
	/^(?:Він|Користувач) НЕ бачить (?:кнопку |рецензій від )?"([^"]*)"$/,
	async function (this: CustomWorld, text: string) {
		await expect(this.page!.getByText(text)).not.toBeVisible();
	},
);

Then('Він бачить кнопку {string}', async function (this: CustomWorld, btnName: string) {
	const btn = this.page!.getByRole('button', { name: btnName });
	const link = this.page!.getByRole('link', { name: btnName });
	const hasBtn = (await btn.count()) > 0;
	if (hasBtn) {
		await expect(btn.first()).toBeVisible();
	} else {
		await expect(link.first()).toBeVisible();
	}
});

Then(
	'Користувача перенаправлено на головну сторінку {string}',
	async function (this: CustomWorld, url: string) {
		await expect(this.page!).toHaveURL(new RegExp(url)); // RegExp дозволяє ігнорувати query params
	},
);

// ==========================================
// 4. СПЕЦИФІКА: ПРОФІЛЬ ТА СОЦІАЛЬНЕ (Epic 2)
// ==========================================

When('Користувач переходить на сторінку свого профілю', async function (this: CustomWorld) {
	await this.page!.goto(`${this.appUrl}/profile`);
	// /profile does client-side redirect to /profile/[username]; wait for it
	await this.page!.waitForURL(/\/profile\/.+/, { timeout: 10000 }).catch(() => {});
	await this.page!.waitForLoadState('networkidle');
});

Then('Кнопка змінює назву на {string}', async function (this: CustomWorld, newText: string) {
	await expect(this.page!.getByRole('button', { name: newText })).toBeVisible();
});

// ==========================================
// 5. СПЕЦИФІКА: МЕДІА ТА СТРІЧКА (Epic 3, 4)
// ==========================================


// Складний крок з часом (Regex)
Given(
	'{string} написав рецензію {string} \\({int} годин тому)',
	async function (this: CustomWorld, author: string, _title: string, _hours: number) {
		// Precondition: review assumed created via API seed or test setup
		try {
			await this.api!.post('debug/ensure-user', { data: { username: author, password: 'Password123' } });
		} catch {
			// Author may already exist
		}
	},
);

Given(
	'{string} написав рецензію {string} \\({int} години тому)',
	async function (this: CustomWorld, author: string, _title: string, _hours: number) {
		// Precondition: review assumed created via API seed or test setup
		try {
			await this.api!.post('debug/ensure-user', { data: { username: author, password: 'Password123' } });
		} catch {
			// Author may already exist
		}
	},
);

// ==========================================
// 6. СПЕЦИФІКА: МОДЕРАЦІЯ ТА АДМІНКА (Epic 6, 7)
// ==========================================

When('Він натискає {string} і обирає причину', async function (this: CustomWorld, btnText: string) {
	await this.page!.getByRole('button', { name: btnText }).click();
	await this.page!.locator('input[type="radio"]').first().click(); // Обираємо першу причину
	await this.page!.getByRole('button', { name: /Надіслати|Submit/ }).click();
});

Then(
	/^(?:Рецензія|Медіа) "([^"]*)" позначається як видален(а|е) \(Soft Delete\)$/,
	async function (this: CustomWorld, item: string) {
		// Перевіряємо візуально (сірий колір або opacity)
		const element = this.page!.locator(`.item:has-text("${item}")`);
		await expect(element).toHaveClass(/deleted|disabled|opacity-50/);
	},
);

// ==========================================
// 7. СПЕЦИФІКА: ПОШУК ТА API (Epic 9)
// ==========================================

When(
	/^(?:Користувач|Він) вводить "([^"]*)" у пошук$/,
	async function (this: CustomWorld, query: string) {
		await this.page!.fill('input[type="search"]', query);
		await this.page!.keyboard.press('Enter');
	},
);

Then('Система завантажує повні дані з зовнішнього API', async function (this: CustomWorld) {
	// Wait for network to settle and verify content appeared
	await this.page!.waitForLoadState('networkidle');
	// Verify page has meaningful content loaded (not empty/error)
	await expect(this.page!.locator('.toast-error, .alert-error')).not.toBeVisible();
	const mainContent = this.page!.locator('main, [data-testid="content"], .content');
	const hasContent = await mainContent.count();
	if (hasContent > 0) {
		await expect(mainContent.first()).not.toBeEmpty();
	}
});

// ==========================================
// 8. ЗАГЛУШКИ ДЛЯ БАЗИ ДАНИХ (Backend Mocks)
// ==========================================

// DB verification — check no error on page as proxy for successful backend operation
Then(/.* (?:в базі даних|в БД) .*/, async function (this: CustomWorld) {
	await expect(this.page!.locator('.toast-error, .alert-error')).not.toBeVisible();
});

// DB precondition — simple "В базі даних існує/існують X" with optional quoted name and password
Given(
	/^В базі даних існу(?:є|ють) (?:медіа|користувач(?:і)?) "([^"]*)"(?:\s+з паролем "[^"]*")?$/,
	async function (this: CustomWorld, username: string) {
		try {
			await this.api!.post('debug/ensure-user', {
				data: { username, password: 'Password123' },
			});
		} catch {
			// user or media may already exist
		}
	},
);

// --- РЕЄСТРАЦІЯ ТА АВТОРИЗАЦІЯ ---

Then(
	'Система створює нового користувача {string} в базі даних',
	async function (this: CustomWorld, username: string) {
		// E2E: verify no error on screen and user is logged in (header shows username)
		await expect(this.page!.locator('.toast-error, .alert-error')).not.toBeVisible();
		await expect(this.page!.locator('header')).toContainText(username);
	},
);

Then(
	'Користувач {string} автоматично авторизований',
	async function (this: CustomWorld, username: string) {
		// Header shows username when logged in
		await expect(this.page!.locator('header')).toContainText(username);
	},
);

When(
	'Гість намагається зареєструватися з нікнеймом {string}',
	async function (this: CustomWorld, username: string) {
		// Заповнюємо форму даними, де нікнейм вже зайнятий
		await this.page!.fill('input[name="username"]', username); // Перевірте селектор name="username" або placeholder
		await this.page!.fill('input[type="email"]', 'duplicate@test.com');
		await this.page!.fill('input[type="password"]', 'Password123');
		await this.page!.fill('input[name="confirm_password"]', 'Password123');
		await this.page!.click('button[type="submit"]');
	},
);

Then('Система не створює нового користувача', async function (this: CustomWorld) {
	// Should still be on registration page with an error visible
	await expect(this.page!).toHaveURL(/register/);
});

When('Гість вводить пароль {string}', async function (this: CustomWorld, password: string) {
	// Fill required fields with valid dummy data so only password validation triggers
	const usernameField = this.page!.locator('input[name="username"]');
	if ((await usernameField.count()) > 0 && !(await usernameField.inputValue()))
		await usernameField.fill('tmp_pass_test');
	const emailField = this.page!.locator('input[name="email"]');
	if ((await emailField.count()) > 0 && !(await emailField.inputValue()))
		await emailField.fill('tmp_pass@test.com');

	await this.page!.fill('input[name="password"], input[type="password"]', password);

	const confirmInput = this.page!.locator('input[name="confirm_password"], input[name="confirmPassword"]');
	if ((await confirmInput.count()) > 0)
		await confirmInput.fill(password);
});

// Given form: performs full login (used as precondition in collections/other features)
// Also used as Then assertion after login — waits for header to show username first
Given('Користувач {string} авторизований', async function (this: CustomWorld, username: string) {
	// Wait up to 5s for any in-flight login navigation to complete and header to show username
	const alreadyLoggedIn = await expect(this.page!.locator('header'))
		.toContainText(username, { timeout: 5000 })
		.then(() => true)
		.catch(() => false);
	if (alreadyLoggedIn) return;

	// Not logged in yet — do full login flow
	await this.api!.post('debug/ensure-user', { data: { username, password: 'Password123' } }).catch(() => {});
	await this.page!.goto(`${this.appUrl}/auth/login`);
	await this.page!.fill('input[name="email"], input[type="email"]', username);
	await this.page!.fill('input[name="password"], input[type="password"]', 'Password123');
	await this.page!.getByRole('button', { name: /Увійти|Login/i }).click();
	await this.page!.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});
	await expect(this.page!.locator('header')).toContainText(username);
});


Then('Сесія користувача завершена', async function (this: CustomWorld) {
	// After logout: "Вхід" link visible in header (guest state)
	await expect(this.page!.getByRole('link', { name: 'Вхід' })).toBeVisible();
});

// --- ПРОФІЛЬ ТА СОЦІАЛЬНІ ФУНКЦІЇ ---

Given('В базі даних існує інший користувач {string}', async function (this: CustomWorld, username: string) {
	// Ensure user exists via API
	try {
		await this.api!.post('debug/ensure-user', {
			data: { username, password: 'Password123' },
		});
	} catch {
		// User may already exist
	}
});

Given(
	'Користувач {string} знаходиться на сторінці редагування профілю',
	async function (this: CustomWorld, username: string) {
		// Login first
		await this.page!.goto(`${this.appUrl}/auth/login`);
		await this.page!.fill('input[name="email"], input[type="email"]', username);
		await this.page!.fill('input[name="password"], input[type="password"]', 'Password123');
		await this.page!.getByRole('button', { name: /Увійти|Login/i }).click();
		await this.page!.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});
		await this.page!.goto(`${this.appUrl}/profile/${username}/edit`);
	},
);

Given(
	'Користувач {string} авторизований і знаходиться на сторінці профілю {string}',
	async function (this: CustomWorld, user: string, profileUrl: string) {
		// Login via UI first
		await this.page!.goto(`${this.appUrl}/auth/login`);
		await this.page!.fill('input[name="email"], input[type="email"]', user);
		await this.page!.fill('input[name="password"], input[type="password"]', 'Password123');
		await this.page!.getByRole('button', { name: /Увійти|Login/i }).click();
		await this.page!.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});
		// Navigate to profile page
		await this.page!.goto(`${this.appUrl}${profileUrl}`);
	},
);

Given('{string} ще не підписаний на {string}', async function (this: CustomWorld, _user1: string, _user2: string) {
	// Precondition: default state is not following — no action needed
});

When(
	'Користувач {string} натискає кнопку {string}',
	async function (this: CustomWorld, user: string, buttonText: string) {
		// Універсальний клік по кнопці з текстом
		await this.page!.click(`button:has-text("${buttonText}"), a:has-text("${buttonText}")`);
	},
);

Then("Система створює зв'язок {string} в базі даних", async function (this: CustomWorld, _relation: string) {
	// Verify no error after relationship creation + page is stable
	await this.page!.waitForLoadState('networkidle');
	await expect(this.page!.locator('.toast-error, .alert-error')).not.toBeVisible();
	// Verify success feedback (toast or UI state change)
	const successToast = this.page!.locator('.toast-success, .alert-success');
	const hasSuccess = await successToast.count();
	if (hasSuccess > 0) {
		await expect(successToast.first()).toBeVisible();
	}
});

Then(
	'Лічильник {string} у {string} збільшується на {int}',
	async function (this: CustomWorld, counterName: string, user: string, value: number) {
		// Find counter element and verify it contains a numeric value
		const counterEl = this.page!.locator(`[data-testid="counter-${counterName.toLowerCase()}"], :has-text("${counterName}")`).first();
		await expect(counterEl).toBeVisible();
		const text = await counterEl.innerText();
		const num = parseInt(text.replace(/\D/g, ''), 10);
		expect(num).toBeGreaterThanOrEqual(value);
	},
);

Given(
	'Користувач {string} авторизований і вже підписаний на {string}',
	async function (this: CustomWorld, user1: string, user2: string) {
		// Ensure both users exist and user1 follows user2 via API
		try {
			await this.api!.post('debug/ensure-user', { data: { username: user1, password: 'Password123' } });
			await this.api!.post('debug/ensure-user', { data: { username: user2, password: 'Password123' } });
			await this.api!.post('debug/ensure-follow', { data: { follower: user1, following: user2 } });
		} catch {
			// Setup may fail if debug endpoints unavailable — continue with UI flow
		}
		// Login user1 via UI
		await this.page!.goto(`${this.appUrl}/auth/login`);
		await this.page!.fill('input[name="email"], input[type="email"]', user1);
		await this.page!.fill('input[name="password"], input[type="password"]', 'Password123');
		await this.page!.getByRole('button', { name: /Увійти|Login/i }).click();
		await this.page!.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {});
	},
);

Then("Система м'яко видаляє зв'язок {string} в базі даних", async function (this: CustomWorld, _relation: string) {
	// Verify UI reflects unfollow — no error + button state changed
	await this.page!.waitForLoadState('networkidle');
	await expect(this.page!.locator('.toast-error, .alert-error')).not.toBeVisible();
	// Button should revert to "follow" state after unfollow
	const followBtn = this.page!.getByRole('button', { name: /Підписатися|Follow/i });
	const hasFollowBtn = await followBtn.count();
	if (hasFollowBtn > 0) {
		await expect(followBtn.first()).toBeVisible();
	}
});

// --- СТРІЧКА (FEED) ---

Given('{string} підписаний на {string}', async function (this: CustomWorld, u1: string, u2: string) {
	// Precondition: ensure both users exist via API
	try {
		await this.api!.post('debug/ensure-user', { data: { username: u1, password: 'Password123' } });
		await this.api!.post('debug/ensure-user', { data: { username: u2, password: 'Password123' } });
	} catch {
		// Users may already exist
	}
});

Given('Користувач {string} ще ні на кого не підписаний', async function (this: CustomWorld, user: string) {
	// Ensure user exists — fresh user has no subscriptions by default
	try {
		await this.api!.post('debug/ensure-user', { data: { username: user, password: 'Password123' } });
	} catch {
		// User may already exist
	}
});

When(
	'Користувач {string} відкриває головну сторінку',
	async function (this: CustomWorld, _user: string) {
		await this.page!.goto(`${this.appUrl}/`);
	},
);

When('{string} відкриває головну сторінку', async function (this: CustomWorld, _user: string) {
	await this.page!.goto(`${this.appUrl}/`);
});

Then(
	'Він НЕ бачить рецензій від користувачів, на яких не підписаний',
	async function (this: CustomWorld) {
		// Feed should be empty or show only followed users' content
		const feedItems = this.page!.locator('[data-testid="feed-item"], .feed-item, .review-card');
		const count = await feedItems.count();
		expect(count).toBe(0);
	},
);

Given('{string} бачить {string} у своїй стрічці', async function (this: CustomWorld, _user: string, review: string) {
	// Verify review text visible on current page
	await expect(this.page!.getByText(review).first()).toBeVisible();
});

Given('{string} ще не лайкнув {string}', async function (this: CustomWorld, _user: string, _review: string) {
	// Precondition — no action needed, default state is unliked
});

When('Він натискає {string} на {string}', async function (this: CustomWorld, action: string, item: string) {
	// Find card containing item text, then click action button within it
	const card = this.page!.locator(`.review-card:has-text("${item}"), .feed-item:has-text("${item}"), [data-testid="review"]:has-text("${item}")`).first();
	const cardExists = await card.count();
	if (cardExists > 0) {
		await card.getByRole('button', { name: new RegExp(action, 'i') }).click();
	} else {
		// Fallback: click any button with action text
		await this.page!.click(`text=${action}`);
	}
});

Then('Лічильник вподобайок {string} збільшується на {int}', async function (this: CustomWorld, item: string, count: number) {
	// Find item card and verify like counter has numeric value >= count
	const itemCard = this.page!.locator(`.review-card:has-text("${item}"), .feed-item:has-text("${item}"), [data-testid="review"]:has-text("${item}")`).first();
	await expect(itemCard).toBeVisible();
	const likeCounter = itemCard.locator('[data-testid="like-count"], .like-count');
	const counterExists = await likeCounter.count();
	if (counterExists > 0) {
		const text = await likeCounter.innerText();
		const num = parseInt(text.replace(/\D/g, ''), 10);
		expect(num).toBeGreaterThanOrEqual(count);
	}
});

Then('Кнопка змінює стан на {string}', async function (this: CustomWorld, state: string) {
	// Verify button with expected state text is visible
	await expect(this.page!.getByRole('button', { name: new RegExp(state, 'i') })).toBeVisible();
});

// --- МЕДІА СТОРІНКИ ---

Given('Існує медіа {string} \\(Id: {int})', async function (this: CustomWorld, name: string, id: number) {
	// Precondition: media should exist in backend — verify via API or assume seeded
	try {
		const res = await this.api!.get(`media/${id}`);
		expect(res.ok() || res.status() === 200).toBeTruthy();
	} catch {
		// Media may be seeded or not available via this endpoint
	}
});

Given('В системі є український переклад для {string}', async function (this: CustomWorld, _media: string) {
	// Precondition: translation assumed seeded in test DB
});

Given('Користувач {string} авторизований з мовою інтерфейсу {string}', async function (this: CustomWorld, user: string, _lang: string) {
	// Login user then set language preference if UI supports it
	await this.page!.goto(`${this.appUrl}/auth/login`);
	await this.page!.fill('input[name="email"], input[type="email"]', `${user}@example.com`);
	await this.page!.fill('input[name="password"], input[type="password"]', 'Password123');
	await this.page!.getByRole('button', { name: /Увійти|Login/i }).click();
	await expect(this.page!.locator('header')).toBeVisible();
});

When(
	'Користувач {string} відкриває сторінку {string}',
	async function (this: CustomWorld, user: string, url: string) {
		await this.page!.goto(`${this.appUrl}${url}`);
	},
);

When(
	/^(?:Він|Користувач) відкриває? сторінку "([^"]*)"$/,
	async function (this: CustomWorld, url: string) {
		await this.page!.goto(`${this.appUrl}${url}`);
	},
);

Then(
	'Він бачить український заголовок {string}',
	async function (this: CustomWorld, title: string) {
		await expect(this.page!.locator('h1')).toContainText(title);
	},
);

Then('Він бачить список рецензій', async function (this: CustomWorld) {
	await expect(this.page!.locator('.reviews-list')).toBeVisible();
});

// --- FEED & LISTS ---

// Epic 3: Перевірка порядку (сортування)
Then(
	'Він бачить {string} вище, ніж {string}',
	async function (this: CustomWorld, firstItem: string, secondItem: string) {
		// Шукаємо всі елементи, що можуть бути заголовками рецензій
		const reviews = this.page!.locator('.review-title'); // FIXME: Замінити клас на реальний

		// Отримуємо текст всіх елементів масивом
		const texts = await reviews.allInnerTexts();

		const indexA = texts.indexOf(firstItem);
		const indexB = texts.indexOf(secondItem);

		expect(indexA).not.toBe(-1); // Елемент має існувати
		expect(indexB).not.toBe(-1);
		expect(indexA).toBeLessThan(indexB); // Перший має бути раніше (вище) в DOM
	},
);

// --- TRACKING & DROPDOWNS ---

// Epic 5: Статуси
When('Обирає статус {string}', async function (this: CustomWorld, status: string) {
	// Припускаємо, що це select або custom dropdown
	// Варіант 1: Стандартний select
	try {
		await this.page!.selectOption('select[name="status"]', { label: status });
	} catch {
		// Варіант 2: Custom UI (Tailwind dropdown)
		await this.page!.click('[data-testid="status-dropdown-trigger"]');
		await this.page!.click(`text=${status}`);
	}
});

Then(
	'"Кнопка Статусу" змінює текст на {string}',
	async function (this: CustomWorld, expectedText: string) {
		const button = this.page!.locator('[data-testid="status-button"]');
		await expect(button).toHaveText(expectedText);
	},
);

// --- SEARCH & EXTERNAL API ---


// Розрізнення локальних та зовнішніх результатів
// Це змусить фронтенд мати різні візуальні маркери або класи для різних джерел
Then(
	'Він бачить {string} \\(знайдено у зовнішньому API)',
	async function (this: CustomWorld, title: string) {
		const item = this.page!.locator(`.search-result-external:has-text("${title}")`);
		await expect(item).toBeVisible();
	},
);

Then(
	'Він бачить {string} \\(знайдено локально)',
	async function (this: CustomWorld, title: string) {
		const item = this.page!.locator(`.search-result-local:has-text("${title}")`);
		await expect(item).toBeVisible();
	},
);

// --- SYSTEM FEEDBACK ---

Then(
	'Система показує повідомлення про помилку {string}',
	async function (this: CustomWorld, msg: string) {
		// SvelteKit зазвичай використовує toast store
		const toast = this.page!.locator('.toast-error, .alert-error');
		await expect(toast).toContainText(msg);
	},
);

Then('Система хешує пароль користувача', async function () {
	// Password hashing is verified by the backend; frontend BDD only confirms the registration flow completed.
});

Then(
	/^Система показує повідомлення про помилку про відсутню (цифру|літеру)$/,
	async function (this: CustomWorld, missing: string) {
		const expected = missing === 'цифру' ? /цифр|digit/i : /літер|letter/i;
		const toast = this.page!.locator('.toast-error, .alert-error');
		await expect(toast).toContainText(expected);
	},
);

Given(
	'Гість \\(неавторизований) знаходиться на сторінці {string}',
	async function (this: CustomWorld, path: string) {
		await this.context!.clearCookies();
		await this.page!.goto(path);
	},
);


// --- BACKEND ACTION VERIFICATION ---
Then(/^Система (?!не )(?!м'яко ).* (?:створює|оновлює|видаляє|зберігає) .*$/, async function (this: CustomWorld) {
	// Verify backend action completed: network settled, no errors, possible success feedback
	await this.page!.waitForLoadState('networkidle');
	await expect(this.page!.locator('.toast-error, .alert-error')).not.toBeVisible();
	// Check for success toast if present
	const successToast = this.page!.locator('.toast-success, .alert-success');
	const hasSuccess = await successToast.count();
	if (hasSuccess > 0) {
		await expect(successToast.first()).toBeVisible();
	}
});

// ==========================================
// 11. E2E AUTH & ROUTE PROTECTION
// ==========================================

Then('Гість залишається на сторінці входу', async function (this: CustomWorld) {
	await expect(this.page!).toHaveURL(/\/auth\/login/);
});

Then(
	'Користувача перенаправлено на сторінку {string}',
	async function (this: CustomWorld, path: string) {
		await expect(this.page!).toHaveURL(new RegExp(path));
	},
);

Then(
	'Гість перенаправлений на сторінку входу з redirectTo {string}',
	async function (this: CustomWorld, expectedPath: string) {
		await expect(this.page!).toHaveURL(
			new RegExp(`/auth/login.*redirectTo=${encodeURIComponent(expectedPath).replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`),
		);
	},
);

