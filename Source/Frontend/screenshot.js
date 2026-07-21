import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext({
    viewport: { width: 1920, height: 1080 }
  });
  const page = await context.newPage();

  console.log("Navigating to Home...");
  await page.goto('http://localhost:5173/');
  await page.waitForTimeout(2000);
  await page.screenshot({ path: '../../Docs/assets/home.png' });
  console.log("Home screenshot saved.");

  console.log("Navigating to Catalog...");
  await page.goto('http://localhost:5173/catalog');
  await page.waitForTimeout(2000);
  await page.screenshot({ path: '../../Docs/assets/catalog.png' });
  console.log("Catalog screenshot saved.");

  console.log("Navigating to Media...");
  await page.goto('http://localhost:5173/media/external/Tmdb:movie:76');
  await page.waitForTimeout(2000);
  await page.screenshot({ path: '../../Docs/assets/media.png' });
  console.log("Media screenshot saved.");

  await browser.close();
})();
