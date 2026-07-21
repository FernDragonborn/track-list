# TrackList — встановлення та запуск

TrackList — це self-host веб-застосунок для трекінгу фільмів, серіалів і книжок: рецензії, коментарі, плейлисти, стрічка підписок. Стек: ASP.NET Core 10 + SvelteKit 2 + SQLite, за reverse proxy Caddy. Розрахований на одного користувача або невелику довірену групу — публічна реєстрація вимкнена за замовчуванням.

Цей документ описує, як підняти інстанс у себе.

---

## 1. Передумови

Для базового запуску потрібен лише **Docker**:

- Docker Desktop (Windows / macOS) **або** Docker Engine + Docker Compose v2 (Linux).
- Git — для клонування репозиторію.
- Вільні порти **80** і **443** на хості (можна перемапити, див. §10).

Для розробки/модифікації коду додатково:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js ≥ 20](https://nodejs.org/) + `corepack enable` (для `pnpm` 11)
- Python 3.10+ (тільки якщо плануєте запускати seeder)

---

## 2. Клонування репозиторію

```bash
git clone https://github.com/<owner>/<repo>.git
cd <repo>/Source
```

Усі команди далі виконуються з каталогу `Source/`.

---

## 3. Налаштування `.env`

Скопіюйте три файли-приклади:

**Linux / macOS:**

```bash
cp .env.example .env
cp Backend/track-list-api/.env.example Backend/track-list-api/.env
cp Frontend/.env.example Frontend/.env
```

**Windows PowerShell:**

```powershell
Copy-Item .env.example .env
Copy-Item Backend/track-list-api/.env.example Backend/track-list-api/.env
Copy-Item Frontend/.env.example Frontend/.env
```

Відкрийте кожний з трьох `.env`-файлів і обовʼязково замініть:

| Змінна | Призначення |
|---|---|
| `JWT_PRIVATE_KEY` | секрет для підпису JWT-токенів; ≥ 64 байт випадкових даних |
| `TRACKLIST_SETUP_TOKEN` | одноразовий токен для створення першого адміна через `/setup` |

Згенерувати випадкові секрети:

```bash
# Linux / macOS
openssl rand -base64 64
```

```powershell
# Windows PowerShell
[Convert]::ToBase64String((1..64 | % { [byte](Get-Random -Max 256) }))
```

Решту значень за замовчуванням можна не чіпати для першого запуску. Деталі про те, які саме змінні дублюються між файлами, описано у блоці `DUPLICATION NOTE` всередині `Source/.env.example`.

---

## 4. Швидкий старт (Docker, production)

```bash
docker compose up --build -d
```

Дочекайтесь, поки контейнери `api`, `web`, `caddy` стануть `healthy` (`docker compose ps`).

Далі:

1. Відкрийте `http://localhost/` у браузері.
2. Якщо в базі ще немає жодного користувача, фронт автоматично перенаправить на `/setup`.
3. Введіть `TRACKLIST_SETUP_TOKEN` (той, що ви задали в `.env`), email і пароль першого адміністратора.
4. Після успіху ви опинитесь на головній сторінці автентифікованим адміном.

**Зупинка:**

```bash
docker compose down            # зберігає базу та завантажені файли
docker compose down -v         # знищує volumes (Caddy дані/конфіг)
rm -rf data/ uploads/          # повне очищення інстансу
```

---

## 5. Зовнішні інтеграції (опційно)

Усі зовнішні HTTP-провайдери вимкнені за замовчуванням. Увімкнення кожного — це **дві дії** у `.env`: підняти прапорець + вписати ключ (де потрібен).

| Інтеграція | Прапорець | Ключ | Що дає |
|---|---|---|---|
| TMDB | `TRACKLIST_ENABLE_TMDB=true` | `TMDBApiKey` | пошук і імпорт фільмів/серіалів |
| OMDb | `TRACKLIST_ENABLE_OMDB=true` | `OMDB_API_KEY` | зовнішні рейтинги IMDb / RT / Metacritic |
| DeepL | `TRACKLIST_ENABLE_DEEPL=true` | `DEEPL_API_KEY` | переклад зовнішніх рев'ю та Wikipedia-анотацій |
| Letterboxd | `TRACKLIST_ENABLE_LETTERBOXD=true` | — | стрічка зовнішніх рев'ю з RSS |
| Wikipedia | `TRACKLIST_ENABLE_WIKIPEDIA=true` | — | додаткова інформація про твір |

Де отримати безкоштовні ключі — посилання у `Source/.env.example`.

> **Зверніть увагу:** при увімкнених інтеграціях назви творів та зовнішні ID, які ви шукаєте, передаються відповідним провайдерам. DeepL додатково отримує текст рев'ю при перекладі.

Після зміни `.env` перезапустіть API-контейнер:

```bash
docker compose restart api
```

---

## 6. Dev-режим (для модифікації коду)

Цей розділ потрібен, лише якщо ви хочете **змінювати** код TrackList. Для звичайного використання достатньо §4.

Підняти бекенд із hot-reload (`dotnet watch`) у Docker і фронтенд (Vite) локально:

```bash
docker compose -f docker-compose.dev.yaml up --build
cd Frontend
pnpm install
pnpm dev
```

- Caddy слухає `:80` і проксіює `/api/*` на бекенд + решту на Vite (`:5173`) згідно з `Caddyfile.dev`.
- Зміни в `Backend/track-list-api/**` підхоплюються `dotnet watch` автоматично.
- Зміни у `Frontend/src/**` — Vite HMR.

---

## 7. Local-режим без Docker (для розробників)

Окремо запустити стек поза контейнерами:

```bash
# Backend (у новій вкладці термінала)
cd Backend/track-list-api
dotnet run            # слухає http://0.0.0.0:8080
```

```bash
# Frontend (у іншій вкладці)
cd Frontend
pnpm install
pnpm dev              # http://localhost:5173
# або production-збірка:
pnpm build && pnpm preview
```

База SQLite (`tracklist.db`) створюється поруч із виконуваним файлом API.

---

## 8. Засіювання тестовими даними (опційно)

Якщо хочете побачити заповнений інтерфейс одразу (\~100 користувачів, \~300 творів, \~2700 рев'ю), у репозиторії є Python-seeder. Це для демо/тестування — у звичайному self-host сценарії не потрібно.

```bash
cd seeder
cp .env.example .env          # вписати TMDB ключ і параметри адміна
python -m venv .venv
source .venv/bin/activate     # Windows: .\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
python seed.py
```

Запускати **після** того, як ви створили першого адміна через `/setup` у §4.

---

## 9. Міграції БД

Міграції накатуються автоматично під час старту API — окремо нічого робити не треба. Якщо потрібно вручну:

```bash
cd Backend/track-list-api
dotnet ef database update
```

---

## 10. Поширені операції

**Логи:**

```bash
docker compose logs -f api
docker compose logs -f web
docker compose logs -f caddy
```

**Скинути першого адміна** (наприклад, якщо забули пароль і не налаштували email-відновлення):

```bash
docker compose down
rm data/tracklist.db*
docker compose up -d
# Знову відкрити http://localhost/ → /setup
```

**Перевірити стан інсталяції з командного рядка:**

```bash
curl http://localhost/api/setup/status
# {"needsSetup": false}  ← адмін вже існує
# {"needsSetup": true}   ← база порожня, чекає /setup
```

**Інший порт (80 уже зайнятий)** — у `docker-compose.yaml` змініть маппінг для сервісу `caddy`:

```yaml
ports:
  - "8080:80"      # хост:контейнер
  - "8443:443"
```

---

## 11. HTTPS-домен

`Caddyfile` уже сконфігурований під автоматичні Let's Encrypt сертифікати. Перший рядок:

```caddy
{$SITE_ADDRESS::80} {
```

читає змінну `SITE_ADDRESS`. Щоб увімкнути HTTPS:

1. Спрямуйте свій домен (A/AAAA-запис) на IP сервера.
2. У `Source/.env` додайте `SITE_ADDRESS=tracklist.example.com`.
3. Перезапустіть `caddy`:
   ```bash
   docker compose up -d --force-recreate caddy
   ```
4. Caddy сам отримає сертифікат від Let's Encrypt при першому запиті.

Також додайте у `.env` ваш домен у `TRACKLIST_ALLOWED_ORIGINS`:

```env
TRACKLIST_ALLOWED_ORIGINS=https://tracklist.example.com
```

---

## 12. Безпековий чекліст self-host

- [ ] `JWT_PRIVATE_KEY` — власне ≥ 64-байтове випадкове значення. Дефолтний placeholder призводить до falsy старту API навмисне.
- [ ] `TRACKLIST_SETUP_TOKEN` — власний випадковий рядок. Після створення першого адміна токен більше не використовується, але не публікуйте його в коммітах.
- [ ] `TRACKLIST_PUBLIC_REGISTRATION` — залиште `false`, якщо не хочете відкривати реєстрацію.
- [ ] `TRACKLIST_MAX_USERS` — підвищіть лише якщо плануєте запросити друзів; за замовчуванням `1`.
- [ ] HTTPS налаштовано через домен у `SITE_ADDRESS` (§11), а не голий HTTP-доступ зовні.
- [ ] `TRACKLIST_ALLOWED_ORIGINS` містить ваш домен.
- [ ] `.env` файли **не закомічені** у Git (вони у `.gitignore`).

Детальніше — у [`Docs/self-host-security.md`](./self-host-security.md).

---

## 13. Траблшутинг

| Симптом | Імовірна причина | Рішення |
|---|---|---|
| API не стартує, лог `JWT_PRIVATE_KEY` | placeholder зі `.env.example` | згенеруйте довгий випадковий ключ (§3) |
| Бінд `:80` падає з `address already in use` | інший процес слухає 80 | перемапте порт у compose (§10) |
| `/setup` відповідає 404 | старий SvelteKit-bundle у контейнері | `docker compose up -d --build web` |
| `SQLite is locked` | бекенд запущений одночасно і в Docker, і локально | залиште один варіант запуску |
| Сторінка завантажується, але API повертає CORS-помилку | домен не в `TRACKLIST_ALLOWED_ORIGINS` | додайте і перезапустіть `api` |
| `/setup` редирект не спрацьовує | у БД уже є користувач, інстанс готовий | відкрийте `/auth/login` |

---

## Звідки взяти допомогу

- Issues у GitHub-репозиторії — для багів і запитів на функції.
- Огляд архітектури — у `CLAUDE.md` в корені `Source/`.
- Документація з безпеки self-host — `Docs/self-host-security.md`.
