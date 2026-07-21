# Lint warnings — план виправлень та прогрес

Tracking-файл для multi-session роботи з ліквідації warnings лінтера.
Повний план: `C:\Users\fern\.claude\plans\harmonic-riding-squirrel.md`.

## Baseline (08.06.2026)

| Контур                              | Warnings |
| ----------------------------------- | -------: |
| Frontend ESLint                     |       88 |
| Frontend svelte-check               |       85 |
| Backend Roslyn + SonarAnalyzer      |       38 |
| **Разом**                           |  **211** |

Команди для отримання поточного зрізу:

```bash
cd Frontend && npm run lint
cd Frontend && npm run check
cd Backend/track-list-api && dotnet build track-list-api.csproj
```

## Партії

Статус: `[ ]` не розпочато · `[~]` у роботі · `[x]` готово · `[!]` заблоковано.

### Backend

- [x] **Партія 1 — Backend Cleanup** (S125, S1134, S1144, S4487, CS1570; 8 warnings) — 2026-06-08
  - Configure.cs (3× S125 — один проявився після переміщення рядків), MediaTranslation.cs (FIXME → TODO),
    TrackListDbContext.cs (видалено `Env` + `using dotenv.net`),
    ExternalReviewerService.cs (видалено `_uow`, `_log` + ctor params),
    ExternalContentService.cs (видалено `_translation` + ctor param),
    LetterboxdRssClient.cs (`&` → `and` в XML doc)
  - Результат: backend warnings **38 → 31** (-7)

- [x] **Партія 2 — Backend Sonar style** (S6580, S1066, S2971, S1155, S3358; 10 warnings) — 2026-06-08
  - **Підпартія 2a [x]** — non-LINQ безпечні фікси (S6580×3, S1066, S3358×2 + S1135 видалений TODO).
    Результат: backend warnings **31 → 24** (-7). Тести 112/112 passed.
    Файли: DateOnlyConverter.cs, ExternalReviewerService.cs, WikipediaClient.cs,
    ExternalReviewerController.cs, ExternalContentService.cs, MediaTranslation.cs.
  - **Підпартія 2b [x]** — ReportService FirstOrDefault refactor (S2971, in-memory ICollection). Backend warnings 24 → 23. Report tests 19/19 passed.
  - **Підпартія 2c [x]** — ExternalContentRefreshService #pragma S2971 з обґрунтуванням у коді. Backend warnings 23 → 22. Тести 169/169 passed.
  - **Підпартія 2d [x]** — MediaGetService S1155 додано до існуючого #pragma з повним обґрунтуванням. Backend warnings 22 → 20. Media tests 17/17 passed.
  - Аудит LINQ-edge cases — `Docs/lint-fix-linq-audit.md`.

- [x] **Партія 3 — Repository Update (CS0108)** (18 warnings) — 2026-06-08
  - 9 пар Repository*.cs / IReposotory/I*Repository.cs.
  - Рішення: `new` keyword + інлайн-коментар з причиною hide замість видалення
    методів, тому що subclass-overload відрізняється сигнатурою (`Task<T>` /
    `Task` замість `Task<Result>`) і використовується викликачами без unwrap.
  - Результат: backend warnings **20 → 2** (-18). Тести 169/169 passed.

- [x] **Партія 4 — Primary constructor capture (CS9107)** (2 warnings) — 2026-06-08
  - PlaylistItemRepository.cs, FollowRepository.cs — primary constructor
    замінено на класичний з явним `_db` полем (як у решті репозиторіїв).
  - Результат: backend warnings **2 → 0** ✅. Тести 169/169 passed.

### Frontend ESLint

- [x] **Партія 5 — ESLint cleanup** (88 warnings) — 2026-06-09
  - `eslint.config.js` — додано `argsIgnorePattern: "^_"`, `varsIgnorePattern: "^_"`,
    `caughtErrorsIgnorePattern: "^_"` → ігнорує всі навмисні `_`-префіксовані символи.
  - `features/support/steps.ts` — префіксовано 15 параметрів (`e`, `title`×2, `hours`×2,
    `user1`, `user2`, `relation`×2, `user`×3, `review`, `media`, `lang`).
  - Видалено 2 невикористані імпорти у prod-коді: `DEFAULT_COLLECTION_NAME`
    (AddToCollectionModal.svelte), `ReviewCardShell` (ReviewCard.svelte).
  - `_id` у MediaPageView.svelte:73 залишено — навмисний reactive trigger для `$effect`,
    тепер ігнорується через `varsIgnorePattern`.
  - Результат: ESLint warnings **88 → 0** ✅. Vitest 18/18 passed.

### Frontend svelte-check

- [x] **Партія 6 — state_referenced_locally** (45 warnings) — 2026-06-09
  - Підхід обрано після аналізу: всі попередження стосуються `$state(prop)` ініціалізації
    локального state, що мутується незалежно від пропа (optimistic UI, infinite scroll,
    форми). `$derived` не підходить — забороняє локальну мутацію. Заміна на
    `$state(untrack(() => prop))` — runtime-ідентична, signal компілятору "snapshot only".
  - Компоненти (7): FollowButton, FeedCard, CommentItem, ReviewCard, ReviewForm,
    ExternalReviewCard, MediaPageView.
  - Routes (11): +page.svelte (home), profile/[username], admin, catalog,
    collections, collections/[id], collections/[id]/settings, external-feed,
    external-reviewers/[handle], following, moderation, reviews.
  - Результат: state_referenced_locally **45 → 0** ✅, svelte-check 85 → 40. Vitest 18/18.

- [x] **Партія 7 — a11y modal/click** (26 warnings) — 2026-06-09
  - Уніфікований патерн на всі 6 модалок: outer div отримує `tabindex={-1}` +
    `onkeydown` що обробляє Escape; click на backdrop використовує
    `e.target === e.currentTarget` замість окремого stopPropagation на inner div.
    Inner панель більше не має click handler — це усунуло warnings без потреби
    додавати keydown або фейковий role.
  - Файли: FollowListModal, ReportModal, AddToCollectionModal, ReviewForm,
    MediaPageView (2 модалки: showAllCollections, showSuggestForm),
    routes/collections/+page.svelte.
  - Окремий `<Modal>` компонент НЕ виносили — поточні 6 модалок різні за
    розміром/контентом, винесення вимагало б snippet-параметрів і ризикувало
    зламати layout. Прямий фікс безпечніший і менший за diff.
  - Результат: a11y modal/click **26 → 0** ✅, svelte-check 40 → 14. Vitest 18/18.

- [x] **Партія 8 — a11y labels** (5 warnings) — 2026-06-09
  - MediaPageView.svelte: input + textarea отримали `id` + `for=""`,
    button group (вибір мови) переведено з `<label>` на `<fieldset>`/`<legend>`.
  - admin/+page.svelte: `id` + `for=""` на двох date inputs.
  - Результат: 5 → 0 ✅.

- [x] **Партія 9 — CSS unused selectors** (8 warnings) — 2026-06-09
  - Клас `.tl-feed-prose` / `.tl-review-prose` передається як проп у `<SafeHtml>`,
    тому Svelte scoped CSS не бачить його у власному шаблоні. Селектори
    перенесено повністю у `:global(.tl-X p) { ... }` — сфера дії та сама,
    лінтер більше не вважає клас неробочим.
  - Файли: FeedCard.svelte, ReviewCard.svelte.
  - Результат: 8 → 0 ✅.

- [x] **Партія 10 — svelte:self deprecated** (1 warning) — 2026-06-09
  - CommentItem.svelte: додано `import Self from './CommentItem.svelte'`,
    `<svelte:self>` замінено на `<Self>`. Іменовано `Self` бо тип `CommentItem`
    вже зайнятий у scope.
  - Результат: 1 → 0 ✅.

## Лог сесій

| Дата | Партія | Стан до | Стан після | Комміт | Примітки |
| ---- | ------ | ------: | ---------: | ------ | -------- |
| 2026-06-08 | baseline | — | 211 (88+85+38) | — | створено tracking-файл |
| 2026-06-08 | Партія 1 | 38 backend | 31 backend | 778cd8c | dead code + FIXME + unused fields + XML doc |
| 2026-06-08 | Партія 2a | 31 backend | 24 backend | 3bc09f6 | S6580 culture, S1066 merge if, S3358 nested ternary, S1135 TODO removed |
| 2026-06-08 | Партія 2b | 24 backend | 23 backend | 6c8aa48 | ReportService Where(p).FirstOrDefault() → FirstOrDefault(p) |
| 2026-06-08 | Партія 2c | 23 backend | 22 backend | b41ac7a | ExternalContentRefreshService #pragma S2971 з документацією |
| 2026-06-08 | Партія 2d | 22 backend | 20 backend | ffdaa2d | MediaGetService #pragma S2971+S1155 з документацією |
| 2026-06-08 | Партія 3 | 20 backend | 2 backend | f180be6 | Repository Update CS0108 → `new` keyword × 18 |
| 2026-06-08 | Партія 4 | 2 backend | **0 backend** ✅ | f180be6 | FollowRepository + PlaylistItemRepository explicit ctor |
| 2026-06-09 | Партія 5 | 88 ESLint | **0 ESLint** ✅ | 8301282 | argsIgnorePattern + steps.ts prefix + 2 unused imports |
| 2026-06-09 | Партія 6 | 85 svelte-check | 40 svelte-check | 85f4f3e | state_referenced_locally → untrack snapshot pattern × 18 файлів |
| 2026-06-09 | Партія 7 | 40 svelte-check | 14 svelte-check | ce23998 | a11y modal/click: tabindex + onkeydown + target===currentTarget pattern × 6 |
| 2026-06-09 | Партія 8 | 14 svelte-check | 9 svelte-check  | (поточний) | a11y labels: id+for=, fieldset/legend for button group |
| 2026-06-09 | Партія 9 | 9 svelte-check  | 1 svelte-check  | (поточний) | CSS unused: full `:global(.tl-X selector)` |
| 2026-06-09 | Партія 10 | 1 svelte-check | **0 svelte-check** ✅ | (поточний) | `<svelte:self>` → `<Self>` self-import |

## Принципи

1. Одна партія = один комміт (атомарність).
2. Lint після кожної партії — переконатись, що зменшення.
3. Тести після ризикових партій (3, 6, 7).
4. Skip-list для свідомих відхилень — додавати `eslint-disable` / SonarAnalyzer suppression
   з обґрунтуванням у коментарі.
5. Не більше 1 партії за сесію.
