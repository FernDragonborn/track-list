import { chromium } from 'playwright';
import fs from 'fs';

(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await context.newPage();

  console.log("Logging in...");
  await page.goto('http://localhost:5173/auth/login');
  await page.fill('#email', 'testy@mail.com');
  await page.fill('#password', '12341234qwe');
  await page.click('button[type="submit"]');
  
  await page.waitForTimeout(2000);
  const content = await page.content();
  fs.writeFileSync('login_error.html', content);
  console.log("Saved login_error.html");
  
  await browser.close();
})();
