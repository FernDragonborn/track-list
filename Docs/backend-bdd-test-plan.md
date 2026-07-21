# Backend BDD Test Plan — Fix 45 Failing Scenarios

## Context

45 Reqnroll BDD tests fail with "No matching step definition found". Features 1, 2, 5 (partial), 10 have `[Binding]` step definition classes and pass. Features 3, 4, 5 (2 scenarios), 6, 7, 8, 9 have `.feature` files + unit tests (`[Fact]`), but **no BDD step definitions**.

**Current state**: 180 pass, 45 fail, 2 skip → **Goal**: 225 pass, 0 fail.

---

## Failure Breakdown

| Feature | File | Failing | New File |
|---------|------|---------|----------|
| 3 — Feed | `3-feed.feature` | 5 | `feedBddTests.cs` |
| 4 — Media page | `4-media_page.feature` | 13 | `mediaPageBddTests.cs` |
| 5 — Tracking | `5-tracking.feature` | 2 | Extend `trackingTests.cs` |
| 6 — Moderation | `6-moderation.feature` | 5 | `moderationBddTests.cs` |
| 7 — Admin | `7-admin.feature` | 6 | `adminBddTests.cs` |
| 8 — Collections | `8-collections.feature` | 9 | `collectionsBddTests.cs` |
| 9 — Search | `9-search.feature` | 5 | `searchBddTests.cs` |
| **Total** | | **45** | **6 new + 1 extended** |

---

## Architecture Pattern

All existing passing BDD tests follow this pattern (from `authenticationTests.cs`, `profileTests.cs`, `reviewTests.cs`, `trackingTests.cs`):

```csharp
[Binding]
[Scope(Feature = "Назва функціоналу")]
public class XxxSteps
{
    private readonly Mock<IService> _serviceMock;
    private readonly XxxController _controller;
    private IActionResult? _lastResult;

    public XxxSteps()
    {
        _serviceMock = new Mock<IService>();
        _controller = new XxxController(_serviceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private void SetUser(string role = "User", string userId = TestConstants.DefaultUserId)
    {
        var claims = new List<Claim>
        {
            new("id", userId),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, "testuser")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    [Given(@"...")] public void Given...() { /* mock setup */ }
    [When(@"...")] public async Task When...() { _lastResult = await _controller.Action(); }
    [Then(@"...")] public void Then...() { Helpers.ThenResponseCodeIs(200, _lastResult); }
}
```

**Key infrastructure**:
- `Helpers.cs` — shared `ThenResponseCodeIs(int, IActionResult?)`
- `TestConstants.cs` — `DefaultUserId`, JWT keys
- Moq for all service/repo mocking
- `Result.Ok()` / `Result.Fail()` for service returns
- `[Scope(Feature = "...")]` to avoid step binding conflicts

---

## Implementation — Phase by Phase

### Phase 1: Feed (5 scenarios)

**File**: `Backend/TrackListTests/feedBddTests.cs`
**Controller**: `FeedController` | **Mock**: `Mock<IFeedService>`

| Scenario | Steps to implement |
|----------|-------------------|
| Перегляд персоналізованої стрічки | Given: users exist, follows set, reviews created. When: GetPersonalFeed(). Then: response contains review_2 before review_1, excludes review_3 |
| Перегляд персоналізованої стрічки (без підписок) | Given: new_user no follows. When: GetPersonalFeed(). Then: empty result with message |
| Перегляд Глобальної стрічки | When: GetGlobalFeed(). Then: all reviews in chronological order |
| Вподобайка рецензії зі стрічки | When: ToggleReviewLike(). Then: like count +1, button state changed |
| Перегляд найзалайканішого коментаря | Then: top comment by likes shown, others hidden |

**Given steps needed** (~12):
- `В базі даних існують користувачі {list}`
- `"X" підписаний на "Y"` (reuse from profileTests via ScenarioContext or re-scope)
- `"X" написав рецензію "Y" на "Z" (N годин тому)`
- `"Y" має "N" вподобайок`
- `"X" залишив коментар "Y" (до "Z") з "N" лайками`
- `Користувач "X" авторизований і бачить "Y" у своїй стрічці`
- `"X" ще не лайкнув "Y"`
- `"X" ще ні на кого не підписаний`

**When steps needed** (~4):
- `Користувач "X" відкриває головну сторінку ("/")`
- `"X" натискає на вкладку "Глобальна стрічка"`
- `Він натискає "Вподобати" (Like) на "X" у стрічці`
- `Він дивиться на блок коментарів під "X"`

**Then steps needed** (~8):
- `Він бачить "X" (від "Y")`
- `Він НЕ бачить "X" (від "Y")`
- `"X" знаходиться у стрічці вище, ніж "Y"`
- `Він НЕ бачить жодної рецензії`
- `Він бачить повідомлення "..."`
- `Лічильник вподобайок "X" оновлюється на "N"`
- `Кнопка змінює стан на "..."`
- `Він бачить посилання "Переглянути всі N коментарів..."`

---

### Phase 2: Media Page (13 scenarios)

**File**: `Backend/TrackListTests/mediaPageBddTests.cs`
**Controllers**: `MediaController`, `ReviewController` | **Mocks**: `Mock<IMediaGetService>`, `Mock<IMediaOperationService>`, `Mock<IReviewService>`

| Scenario | Core logic |
|----------|-----------|
| Сторінка мовою інтерфейсу | GetMediaById → returns uk translation "Дюна" |
| Дефолтна мова (fallback) | GetMediaById → no "pl" → returns en translation "Dune" |
| Нова рецензія з rich-text | CreateReview with HTML content → 201 |
| Друга рецензія (BRL-4) | CreateReview → Fail("already exists") |
| Лайк рецензії | ToggleReviewLike → isLiked:true, count +1 |
| Знімає вподобайку | ToggleReviewLike → isLiked:false, count -1 |
| Новий коментар (Рівень 0) | CreateComment(parentId:null) → 200 |
| Відповідь (Рівень 1) | CreateComment(parentId:comment_1) → 200 |
| Немає Рівня 2 | CreateComment(parentId:reply_1) → Fail |
| Гість взаємодіє з рецензією | No auth → redirect /login |
| Гість коментує | No auth → field disabled |
| Пропонує переклад | AddTranslation(Status:Pending) → 200 |
| Переклад вже існує | AddTranslation → Fail("already exists") |

**Given steps needed** (~15):
- `Існує медіа "X" (Id: N)` — mock media in service
- `В MediaTranslations існує: (MediaId: N, Lang: "X", Title: "Y", Description: "Z")`
- `Мова інтерфейсу користувача "X" встановлена на "Y"`
- `"X" написав рецензію "Y" на "Z" (Id: N)`
- `"X" написав коментар "Y" до рецензії "Z"`
- `Існує "reply_1" (відповідь Рівня 1) на "comment_1"`
- `Користувач "X" авторизований і знаходиться на "/media/N"`
- `"X" ще не писав рецензію на "Y"`
- `"X" вже писав рецензію на "Y"`
- `"X" ще не лайкнув "Y"` / `"X" вже лайкнув "Y"`
- `"Y" має "N" вподобайок`
- `Гість (неавторизований) знаходиться на "/media/N"`
- `Для "X" відсутній/існує переклад "Y"`

**When steps needed** (~8):
- `Користувач "X" відкриває сторінку "/media/N"`
- `Він ставить оцінку "N зірок"`
- `Він вводить в "rich-text" редактор текст: "..."`
- `Він натискає кнопку "Опублікувати рецензію"` / `"Відправити"` / `"Надіслати на модерацію"`
- `Він натискає "Вподобати"/"Вподобано" на "X"`
- `Він вводить "..." у поле коментування під "X"`
- `Гість натискає "Вподобати" на "X"`
- `Гість намагається ввести текст у поле коментування під "X"`

**Then steps needed** (~12):
- `Він бачить заголовок "X"` / `опис "X"`
- `Він бачить рейтинги IMdB / Rotten Tomatoes`
- `Він бачить список рецензій, включаючи "X"`
- `Система зберігає нову рецензію з HTML/Markdown`
- `Його рецензія з'являється у списку`
- `Він бачить повідомлення "..."` / `Він НЕ бачить форми`
- `Лічильник вподобайок "X" стає "N"`
- `"X" з'являється у списку коментарів під "Y"`
- `"X" з'являється під "Y" (з візуальним відступом)`
- `Він НЕ бачить кнопку "Відповісти" біля "X"`
- `Його перенаправляє на сторінку "/login"`
- `Система створює запис у MediaTranslations (Status: "Pending")`

---

### Phase 3: Tracking Extensions (2 scenarios)

**File**: Extend `Backend/TrackListTests/trackingTests.cs`

| Scenario | Missing steps |
|----------|-------------|
| Обирає той самий статус | `У випадаючому меню він обирає статус "Заплановано" (той самий, що й активний)` — verify no backend request |
| Оновлює прогрес серіалу | `Він вводить "5" у поле "Поточний епізод"` + `Система оновлює запис додавши "Progress: 5"` |

Only ~4 new step bindings needed. Add to existing `TrackingTests` class.

---

### Phase 4: Moderation (5 scenarios)

**File**: `Backend/TrackListTests/moderationBddTests.cs`
**Controllers**: `ReportController`, `ModerationController` | **Mocks**: `Mock<IReportService>`, `Mock<IMediaOperationService>`, `Mock<IUnitOfWork>`

| Scenario | Core logic |
|----------|-----------|
| Скаржиться на рецензію | CreateReport(Reason:Spam) → 201, Status:Pending |
| Модератор видаляє контент | ResolveReport(ResolvedDeleted) → DeletedAt set, ProcessedByUserId set |
| Модератор відхиляє скаргу | ResolveReport(ResolvedDismissed) → DeletedAt stays null |
| Схвалює переклад | UpdateTranslationStatus(Approved) → 204, ProcessedByUserId set |
| Відхиляє переклад | UpdateTranslationStatus(Rejected) → 204 |

**Given steps needed** (~6):
- `Існує "X" (Роль: Y)` / `Існує "X" (Роль: Y) з ID "Z"`
- `"X" написав рецензію "Y"`
- `"X" запропонував переклад "Y" (Lang: "Z") ... зі статусом "Pending"`
- `"X" авторизований і знаходиться в "Панелі модератора"`
- `Він бачить скаргу "X" на "Y" (Статус: "Pending")`
- `Він бачить запит "X" зі статусом "Pending"`

**When steps needed** (~5):
- `Він натискає "Поскаржитись"/"Видалити рецензію"/"Відхилити скаргу"/"Схвалити"/"Відхилити"`
- `Він обирає причину "Спам"`
- `Він натискає "Надіслати скаргу"`
- `Він переходить у чергу "Запропоновані переклади"`

**Then steps needed** (~6):
- `Система створює запис у Reports (Status: "Pending")`
- `Поле "DeletedAt" для "X" встановлюється/залишається "null"`
- `Статус скарги "X" оновлюється на "Y"`
- `Поле "ProcessedByUserId" встановлюється на "X"`
- `Статус "X" оновлюється на "Approved"/"Rejected"`
- `Запит зникає з черги модерації`

---

### Phase 5: Admin (6 scenarios)

**File**: `Backend/TrackListTests/adminBddTests.cs`
**Controller**: `AdminController` | **Mocks**: `Mock<IUnitOfWork>`, `Mock<IUserService>`

| Scenario | Core logic |
|----------|-----------|
| Змінює роль (→Модератор) | UpdateRole("Moderator") → Role updated, UpdatedAt set |
| М'яко видаляє користувача | DeleteUser → DeletedAt set, can't login |
| Редагує переклад | UpdateTranslation → Title changed, ProcessedByUserId set |
| М'яко видаляє медіа | DeleteMedia → DeletedAt set, disappears from search |
| Переглядає статистику | GetStats → returns widget data |
| Фільтрує + завантажує звіт | GetStats(dateRange) + ExportCsv → FileContentResult |

**Given steps needed** (~5):
- `"admin" (ID: "X") авторизований і знаходиться в "Панелі адміністратора"`
- `Він бачить переклад "X" (Lang: "Y", Title: "Z", Status: "W")`
- `"admin" знаходиться на сторінці "Статистика"`
- `Він обирає проміжок часу (...)`

**When steps needed** (~8):
- `Він переходить у "Керування користувачами"/"Керування медіа"/"Статистика"`
- `Він знаходить "X" (ID: "Y")`
- `Він натискає "Змінити роль"/"Видалити"/"Редагувати"/"Зберегти"/"Оновити"/"Завантажити звіт"`
- `Він обирає "Модератор"`
- `Він підтверджує дію`
- `Він змінює "Title" на "X"`

**Then steps needed** (~7):
- `Поле "Role" для "X" оновлюється на "Y"`
- `Поле "UpdatedAt" для "X" оновлено`
- `Поле "DeletedAt" для "X" встановлюється на поточний час`
- `Користувач "X" більше не може авторизуватися`
- `"X" зникає зі списків / з пошуку`
- `Він бачить віджети: "..."`
- `Система генерує та завантажує файл (.csv або .xlsx)`

---

### Phase 6: Collections (9 scenarios)

**File**: `Backend/TrackListTests/collectionsBddTests.cs`
**Controller**: `CollectionController` | **Mock**: `Mock<ICollectionService>`

| Scenario | Core logic |
|----------|-----------|
| Створює новий список | Create → OwnerId set, PrivacyLevel:Public default |
| Додає медіа | AddItem(CollectionId, MediaId) → 200 |
| Видаляє медіа (soft) | RemoveItem → DeletedAt set |
| Робить приватним | Update(PrivacyLevel:Private) → 204 |
| Сторонній не бачить | GetByOwner → private excluded for stranger |
| Надає доступ user_B | GrantAccess → CollectionAccess record created |
| Запрошений бачить | GetByOwner → private visible for shared user |
| Забирає доступ | RevokeAccess → record deleted |
| М'яко видаляє список | Delete → DeletedAt set |

**Given steps needed** (~8):
- `В базі даних існує користувач "X" (Роль) з ID "Y"`
- `В базі даних існує медіа "X" (Id: N)`
- `"X" (Власник) авторизований в системі`
- `"X" створив список "Y"` / `"X" є власником списку "Y"`
- `"X" додав "Y" до списку "Z"`
- `"X" надав "Y" доступ до списку "Z"`
- `"X" ще не має доступу до "Y"`

**When steps needed** (~8):
- `Він натискає "Створити список"/"Створити"/"Видалити"/"Надати доступ"/"Видалити доступ"`
- `Він вводить назву/опис "..."`
- `Він обирає "X" зі списку`
- `Він змінює базовий рівень з "X" на "Y"`
- `"X" (Сторонній/Запрошений) переходить на профіль "/profile/Y"`
- `Він відкриває модальне вікно "Налаштувати доступ"`

**Then steps needed** (~8):
- `Система створює новий запис "X" у таблиці Collections`
- `Поле "OwnerId"/"PrivacyLevel"/"UpdatedAt"/"DeletedAt" для "X" встановлено на "Y"`
- `Система створює запис у CollectionItems/CollectionAccess`
- `"X" з'являється/НЕ бачить "Y" у списку`
- `Запис видаляється з CollectionAccess`

---

### Phase 7: Search (5 scenarios)

**File**: `Backend/TrackListTests/searchBddTests.cs`
**Controller**: `MediaController` | **Mocks**: `Mock<IMediaGetService>`, `Mock<IMediaOperationService>`

| Scenario | Core logic |
|----------|-----------|
| Шукає "Dune" — зведені результати | Search("Dune") → results from API |
| Шукає "Бійцівський" — локальна БД | Search("Бійцівський") → Official/Approved only |
| Не знаходить Pending/Deleted | Search("Клуб забіяк") → excluded, Search("Deleted Movie") → excluded |
| Відкриває нове медіа (cache miss) | GetMediaById(externalId) → fetches + creates local record |
| Відкриває кешоване медіа | GetMediaById(internalId) → returns cached, no API call |

**Given steps needed** (~6):
- `Система підключена до зовнішнього API (TMDB)`
- `В таблиці Media існує запис (Id: N, ExternalApiId: "X")`
- `В MediaTranslations існує: (MediaId: N, Lang: "X", Title: "Y", Status: "Z")`
- `В зовнішньому API існує медіа "X" (ExternalApiId: "Y")`
- `"X" ще не існує в локальній таблиці Media`
- `В таблиці Media існує "X" (Id: N, DeletedAt: "Y")`

**When steps needed** (~2):
- `Він вводить запит "X"` (→ controller.Search)
- `Він натискає на "X"` (→ controller.GetMediaById)

**Then steps needed** (~6):
- `Бекенд опитує і локальну БД, і зовнішнє API`
- `Система показує список результатів, що містить: (table)`
- `Він бачить/НЕ бачить "X" у результатах пошуку`
- `Бекенд звертається/НЕ звертається до зовнішнього API`
- `Бекенд створює новий запис в таблиці Media`
- `Бекенд створює запис в MediaTranslations (Status: "Official")`

---

## Step Reuse Strategy

Many Given steps overlap (e.g., "В базі даних існує користувач"). Use `[Scope(Feature = "...")]` on each binding class to avoid conflicts with existing step defs in `authenticationTests.cs`, `profileTests.cs`, `reviewTests.cs`, `trackingTests.cs`.

Shared steps that could be extracted to a common `SharedSteps.cs` class:
- User existence + authorization setup
- Media existence setup
- Response code assertions

**Decision**: Scope per-feature first (simpler, no refactor risk), extract shared later if needed.

---

## Dependencies

- **No production code changes** — only new test files
- Existing: `Helpers.cs`, `TestConstants.cs` — reuse as-is
- All controllers, services, DTOs, models from `api` namespace already exist

---

## Verification

```bash
cd Source && dotnet test Backend/TrackListTests/TrackListTests.csproj --verbosity minimal
```

**Target**: 0 failed, 225+ passed (currently: 45 failed, 180 passed, 2 skipped)

---

## Summary

| Phase | Feature | Scenarios | New Bindings | Priority |
|-------|---------|-----------|-------------|----------|
| 1 | Feed | 5 | ~24 | HIGH |
| 2 | Media Page | 13 | ~35 | HIGH |
| 3 | Tracking (extend) | 2 | ~4 | LOW |
| 4 | Moderation | 5 | ~17 | MEDIUM |
| 5 | Admin | 6 | ~20 | MEDIUM |
| 6 | Collections | 9 | ~24 | HIGH |
| 7 | Search | 5 | ~14 | MEDIUM |
| **Total** | | **45** | **~138** | |
