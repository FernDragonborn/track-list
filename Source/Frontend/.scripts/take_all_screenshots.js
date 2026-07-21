import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await context.newPage();
  const basePath = './Docs/assets';

  console.log("Logging in as testy@mail.com...");
  await page.goto('http://localhost:5174/auth/login');
  await page.fill('#email', 'testy@mail.com');
  await page.fill('#password', '123123qwe');
  await page.click('button[type="submit"]');
  await page.waitForNavigation({ waitUntil: 'networkidle', timeout: 10000 }).catch(() => {});
  console.log("Logged in successfully!");

  // Seed Data Workflow
  const seedItems = [
    { url: 'http://localhost:5174/media/external/Tmdb:movie:76', status: 'Переглянуто', coll: 'Sci-Fi Movies' },
    { url: 'http://localhost:5174/media/external/Tmdb:movie:603', status: 'У планах', coll: 'Sci-Fi Movies' },
    { url: 'http://localhost:5174/media/external/Tmdb:tv:1399', status: 'Дивлюся', coll: 'Epic Series' },
    { url: 'http://localhost:5174/media/external/Tmdb:tv:66732', status: 'Кинуто', coll: '' },
    { url: 'http://localhost:5174/media/external/Tmdb:movie:155', status: 'Переглянуто', coll: 'Masterpieces' }
  ];

  for (const item of seedItems) {
    console.log(`Seeding ${item.url} -> ${item.status}`);
    await page.goto(item.url, { waitUntil: 'networkidle' });
    await page.waitForTimeout(1500);

    // Set Status
    const statusBtn = page.getByRole('button', { name: /Відмітити статус|У планах|Дивлюся|Переглянуто|Кинуто/ }).first();
    if (await statusBtn.isVisible()) {
        const text = await statusBtn.textContent();
        if (!text.includes(item.status)) {
            await statusBtn.click();
            await page.waitForTimeout(500);
            await page.getByRole('button', { name: item.status, exact: true }).first().click();
            await page.waitForTimeout(1000);
        }
    }

    // Add to Collection
    if (item.coll) {
        const collBtn = page.getByRole('button', { name: '+ Додати до добірки' }).first();
        if (await collBtn.isVisible()) {
            await collBtn.click();
            await page.waitForTimeout(1000);
            
            // Check if collection already exists in the list
            const existingAddBtn = page.locator(`div:has-text("${item.coll}")`).locator('button:has-text("Додати")').first();
            if (await existingAddBtn.isVisible()) {
                await existingAddBtn.click();
                await page.waitForTimeout(500);
            } else {
                // If the item is already added to the collection, there will be a "Видалити" button. We can skip.
                const removeBtn = page.locator(`div:has-text("${item.coll}")`).locator('button:has-text("Видалити")').first();
                if (!(await removeBtn.isVisible())) {
                    // Create new
                    await page.getByRole('button', { name: '＋ Нова добірка' }).first().click();
                    await page.waitForTimeout(500);
                    const inputs = page.locator('input[type="text"]');
                    const count = await inputs.count();
                    await inputs.nth(count - 1).fill(item.coll);
                    await page.getByRole('button', { name: 'Створити', exact: true }).first().click();
                    await page.waitForTimeout(1500);
                }
            }
            await page.keyboard.press('Escape');
            await page.waitForTimeout(500);
        }
    }
  }

  console.log("Seeding complete! Capturing screenshots...");

  const screenshots = [
    { name: 'discovery_catalog.png', url: 'http://localhost:5174/catalog' },
    { name: 'discovery_media.png', url: 'http://localhost:5174/media/external/Tmdb:movie:76' },
    { name: 'tracking_list.png', url: 'http://localhost:5174/tracking' },
    { name: 'tracking_collections.png', url: 'http://localhost:5174/collections' },
    { name: 'social_feed.png', url: 'http://localhost:5174/external-feed' },
    { name: 'social_reviews.png', url: 'http://localhost:5174/reviews' },
    { name: 'user_profile.png', url: 'http://localhost:5174/profile' },
    { name: 'user_following.png', url: 'http://localhost:5174/following' },
    { name: 'user_settings.png', url: 'http://localhost:5174/setup' }
  ];

  for (const shot of screenshots) {
    console.log(`Navigating to ${shot.url}...`);
    try {
      await page.goto(shot.url, { waitUntil: 'networkidle', timeout: 5000 });
    } catch(e) {
      console.log(`Timeout waiting for network idle on ${shot.url}`);
    }
    await page.waitForTimeout(2000); // Wait 2s for UI to settle
    await page.screenshot({ path: `${basePath}/${shot.name}` });
    console.log(`Saved ${shot.name}`);
  }

  await browser.close();
})();
