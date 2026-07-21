import { setDefaultTimeout, setWorldConstructor, World } from '@cucumber/cucumber';
import type { IWorldOptions } from '@cucumber/cucumber'; // Додаємо 'type'
import { chromium, request, expect as baseExpect } from '@playwright/test';
import type { Browser, BrowserContext, Page, APIRequestContext } from '@playwright/test'; // Додаємо 'type'

export const expect = baseExpect.configure({ timeout: 15000 });
setDefaultTimeout(30 * 1000);

export class CustomWorld extends World {
	browser: Browser | null = null;
	context: BrowserContext | null = null;
	page: Page | null = null;
	api: APIRequestContext | null = null; // Додаємо API контекст

	appUrl: string;
	apiUrl: string; // URL твого бекенду

	constructor(options: IWorldOptions) {
		super(options);
		this.appUrl = options.parameters.appUrl || 'http://localhost:5173';
		this.apiUrl = options.parameters.apiUrl || 'http://localhost:80/api/';
	}

	async openBrowser() {
		this.browser = await chromium.launch({ headless: true });
		this.context = await this.browser.newContext();
		this.page = await this.context.newPage();
		this.page.setDefaultTimeout(20000);
		this.page.setDefaultNavigationTimeout(20000);

		// Ініціалізуємо API клієнт
		this.api = await request.newContext({
			baseURL: this.apiUrl,
			extraHTTPHeaders: {
				Accept: 'application/json',
			},
		});
	}

	async closeBrowser() {
		await this.api?.dispose(); // Закриваємо з'єднання з API
		await this.page?.close();
		await this.context?.close();
		await this.browser?.close();
	}
}

setWorldConstructor(CustomWorld);
