# LINQ-warnings: аудит з контекстом перед заміною

Перед застосуванням Партії 2 — детальний розбір усіх LINQ-flagged warnings.
Кожен тестуємо окремим коммітом + smoke-тест відповідного API endpoint.

## ПОЛІТИКА ДЛЯ ПРИГНІЧЕННЯ АНАЛІЗАТОРА

**Жодного `#pragma warning disable` без обґрунтування у коді.**

Кожен suppress супроводжується багаторядковим коментарем безпосередньо
над `#pragma warning disable`, що пояснює:
1. яке правило пригнічене (S____);
2. чому не можна виконати рекомендацію аналізатора (LINQ-контекст,
   EF expression tree, semantic difference, performance);
3. короткий приклад того, що зламається при «виправленні».

Те саме стосується ESLint `// eslint-disable-next-line` — коментар має пояснювати
причину, а не просто заглушати правило.

## 1. ExternalContentRefreshService.cs:105 — S2971 (`Count()` → `Count`)

**Контекст:**
```csharp
var topIds = await db.Media
    .OrderByDescending(m => m.Reviews.Count())
    .Select(m => m.Id)
    .Take(TopN)
    .ToListAsync(ct);
```

**Аналіз:**
- `db.Media` — `IQueryable<Media>` (EF Core).
- Вираз `m => m.Reviews.Count()` — частина expression tree, EF Core транслює його
  в SQL `COUNT(*)` через підзапит.
- `m.Reviews.Count` (property) — це property `ICollection<Review>.Count`. У EF
  expression tree спроба використати property замість методу або кидає
  exception "could not be translated", або призводить до повного завантаження
  колекції в пам'ять (катастрофа: для всіх Media завантажить всі Reviews).

**Вердикт: НЕ ЗМІНЮВАТИ.** Sonar помиляється — це false positive у EF-контексті.
**Дія:** додати `#pragma warning disable S2971` навколо рядка з коментарем
"EF expression tree: .Count() needed for SQL COUNT translation".

**Тест після правки:** `GET /api/external-content/refresh-top` (background job)
має повернути N media у тому ж порядку, що й раніше. Перевірити SQL у логах
Serilog — має бути `ORDER BY (SELECT COUNT(*) FROM Reviews ...)`.

---

## 2. ReportService.cs:101 — S2971 (`Where(p).FirstOrDefault()` → `FirstOrDefault(p)`)

**Контекст:**
```csharp
var media = mediaRes.Value;
var title = media.Translations
    .Where(t => t.Status is TranslationStatus.Official or TranslationStatus.Approved)
    .FirstOrDefault()?.Title
    ?? media.Translations.FirstOrDefault()?.Title;
```

**Аналіз:**
- `media.Translations` — це навігаційна властивість EF, **завантажена в пам'ять**
  через `.GetOneAsync(m => m.Id == targetId, "Translations")` (Include + Materialized).
- Тип — `ICollection<MediaTranslation>` → enumeration вже відбулась.
- `Where(p).FirstOrDefault()` та `FirstOrDefault(p)` семантично ідентичні для
  in-memory IEnumerable. Sonar правильний.

**Вердикт: БЕЗПЕЧНО ЗМІНИТИ.**
**Дія:**
```csharp
var title = media.Translations
    .FirstOrDefault(t => t.Status is TranslationStatus.Official or TranslationStatus.Approved)?.Title
    ?? media.Translations.FirstOrDefault()?.Title;
```

**Тест після правки:** `GET /api/reports/{id}` з reportable type=Media. У відповіді
полю `targetNavigation.contentExcerpt` має повернутись назва українською (якщо
є Official/Approved переклад) або перший доступний.

---

## 3. MediaGetService.cs:51 — S1155 (`Count() == 0` → `!Any()`)

**Контекст:**
```csharp
#pragma warning disable S2971 // EF expression tree
"rating_desc" => q => q.OrderByDescending(media =>
    media.Reviews.Count() == 0
        ? 0.0
        : ((double)media.Reviews.Count() / (media.Reviews.Count() + m)) * media.Reviews.Average(r => (double)r.Rating)
          + ((double)m / (media.Reviews.Count() + m)) * C).ThenBy(media => media.Id),
```

**Аналіз:**
- Усередині EF expression tree. Той самий ризик, що й #1.
- `media.Reviews.Count() == 0` → SQL `COUNT(*) = 0`.
- `!media.Reviews.Any()` → SQL `NOT EXISTS(...)`.
- EF Core 8+ підтримує переклад **обох** конструкцій. `.Any()` ефективніший
  (short-circuit на першому рядку без COUNT-агрегації).
- АЛЕ — ризик: рядок 51 використовується в OrderByDescending, де решта виразу
  (`(double)media.Reviews.Count() / ...`) ВСЕ ОДНО потребує `.Count()`. Зміна
  лише першої умови може зробити вираз менш консистентним для читання, але
  технічно валідним.

**Вердикт: МОЖНА ЗМІНИТИ, але без виграшу.** Pragma вже відключає S2971,
тому весь блок ігнорується. S1155 — окреме правило, його теж треба додати в pragma.

**Дія (рекомендую):** додати `S1155` до існуючого `#pragma warning disable`:
```csharp
#pragma warning disable S2971, S1155 // EF expression tree
```

**Альтернатива** (якщо все ж міняти): замінити `Count() == 0` на `!Any()`:
```csharp
!media.Reviews.Any()
    ? 0.0
    : ((double)media.Reviews.Count() / ...
```
Решта Count()-викликів лишаються — без них формула Байєсівського середнього
рейтингу не буде транслюватись.

**Тест після правки:** `GET /api/media?sort=rating_desc&pageSize=20` має
повернути медіа в тому ж порядку (порівняти топ-5 ID з попередньою версією).
Те саме для `rating_asc`. Перевірити, що EF Core не кидає `InvalidOperationException`
при ToListAsync.

---

## 4. MediaGetService.cs:56 — S1155 (`Count() == 0` → `!Any()`)

**Контекст:** Дзеркало #3, для `rating_asc`.

**Вердикт + дія + тест:** ідентичні #3.

---

## Не-LINQ warnings Партії 2 (для повноти)

Користувач замовив акцент на LINQ. Ці фіксимо окремо, з власними коммітами:

- **S6580 × 3** (`DateOnly.Parse` без CultureInfo): низькоризикові, додати
  `CultureInfo.InvariantCulture`.
- **S1066** (merge if у WikipediaClient): тривіально.
- **S3358 × 2** (nested ternary у ExternalReviewerController, ExternalContentService):
  винести у локальну змінну.

Жодних LINQ-побічних ефектів.

---

## Порядок виконання Партії 2

1. **Комміт 2a — non-LINQ безпечні фікси:** S6580×3 + S1066 + S3358×2.
   Перевірка: `dotnet build` -6 warnings.
2. **Комміт 2b — ReportService FirstOrDefault refactor (S2971).**
   Перевірка: `dotnet build` -1 warning + manual GET /api/reports.
3. **Комміт 2c — ExternalContentRefreshService pragma S2971.**
   Перевірка: `dotnet build` -1 warning + manual trigger refresh job + перевірка
   SQL-логу.
4. **Комміт 2d — MediaGetService pragma S1155.**
   Перевірка: `dotnet build` -2 warnings + manual GET /api/media?sort=rating_*.

Очікуваний кінцевий результат після Партії 2: backend warnings 31 → 21.
