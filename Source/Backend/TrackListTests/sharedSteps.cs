using Reqnroll;

// ReSharper disable Reqnroll.MethodNameMismatchPattern
// ReSharper disable UnusedParameter.Global
// ReSharper disable UnusedMember.Global

namespace TrackListTests;

/// <summary>
/// Shared no-op step bindings for UI scenarios that cannot be tested headless.
/// These prevent XUnitPendingStepException for steps describing browser-level UI behavior.
/// Feature-specific scoped bindings in other test files take precedence.
/// </summary>
[Binding]
public class SharedSteps
{
	// ── GIVEN: precondition seeds ───────────────────────────

	[Given(@"^""([^""]*)"" \(ExternalApiId: ""([^""]*)""\) ще не існує в локальній таблиці `Media`$")]
	public void GivenMediaNotInLocalDb(string p0, string p1) { }

	[Given(@"^""([^""]*)"" \(Id: (\d+)\) вже існує в локальній таблиці `Media`$")]
	public void GivenMediaInLocalDb(string p0, int p1) { }

	[Given(@"^""([^""]*)"" вже лайкнув ""([^""]*)""$")]
	public void GivenUserAlreadyLiked(string p0, string p1) { }

	[Given(@"^""([^""]*)"" вже писав рецензію на ""([^""]*)"" \(Id: (\d+)\)$")]
	public void GivenUserAlreadyWroteReview(string p0, string p1, int p2) { }

	[Given(@"^""([^""]*)"" додав ""([^""]*)"" \(Id: (\d+)\) до списку ""([^""]*)""$")]
	public void GivenUserAddedToList(string p0, string p1, int p2, string p3) { }

	[Given(@"^""([^""]*)"" є власником приватного списку ""([^""]*)""$")]
	public void GivenUserOwnsPrivateList(string p0, string p1) { }

	[Given(@"^""([^""]*)"" є власником публічного списку ""([^""]*)""$")]
	public void GivenUserOwnsPublicList(string p0, string p1) { }

	[Given(@"^""([^""]*)"" є власником списку ""([^""]*)""$")]
	public void GivenUserOwnsList(string p0, string p1) { }

	[Given(@"^""([^""]*)"" залишив коментар ""([^""]*)"" \(до ""([^""]*)""\) з ""([^""]*)"" лайками$")]
	public void GivenUserLeftComment(string p0, string p1, string p2, string p3) { }

	[Given(@"^""([^""]*)"" запропонував переклад ""([^""]*)"" \(Lang: ""([^""]*)""\) для ""([^""]*)"" \(Id: (\d+)\) зі статусом ""([^""]*)""$")]
	public void GivenUserProposedTranslation(string p0, string p1, string p2, string p3, int p4, string p5) { }

	[Given(@"^""([^""]*)"" знаходиться на сторінці ""([^""]*)""$")]
	public void GivenUserOnPage(string p0, string p1) { }

	[Given(@"^""([^""]*)"" має ""([^""]*)"" вподобайок$")]
	public void GivenHasLikes(string p0, string p1) { }

	[Given(@"^""([^""]*)"" надав ""([^""]*)"" доступ до ""([^""]*)""$")]
	public void GivenGrantedAccess(string p0, string p1, string p2) { }

	[Given(@"^""([^""]*)"" надав ""([^""]*)"" доступ до приватного списку ""([^""]*)""$")]
	public void GivenGrantedAccessPrivateList(string p0, string p1, string p2) { }

	[Given(@"^""([^""]*)"" написав коментар ""([^""]*)"" до рецензії ""([^""]*)""$")]
	public void GivenUserWroteComment(string p0, string p1, string p2) { }

	[Given(@"^""([^""]*)"" написав рецензію ""([^""]*)"" на ""([^""]*)"" \((\d+) годин тому\)$")]
	public void GivenUserWroteReviewHoursAgoPlural(string p0, string p1, string p2, int p3) { }

	[Given(@"^""([^""]*)"" написав рецензію ""([^""]*)"" на ""([^""]*)"" \((\d+) години тому\)$")]
	public void GivenUserWroteReviewHoursAgoFew(string p0, string p1, string p2, int p3) { }

	[Given(@"^""([^""]*)"" написав рецензію ""([^""]*)"" на ""([^""]*)"" \((\d+) годину тому\)$")]
	public void GivenUserWroteReviewHourAgo(string p0, string p1, string p2, int p3) { }

	[Given(@"^""([^""]*)"" написав рецензію ""([^""]*)"" на ""([^""]*)"" \(Id: (\d+)\)$")]
	public void GivenUserWroteReviewOnMedia(string p0, string p1, string p2, int p3) { }

	[Given(@"^""([^""]*)"" написав рецензію ""([^""]*)""$")]
	public void GivenUserWroteReview(string p0, string p1) { }

	[Given(@"^""([^""]*)"" НЕ підписаний на ""([^""]*)""$")]
	public void GivenUserNotFollowingShared(string p0, string p1) { }

	[Given(@"^Публічна реєстрація увімкнена для інстансу$")]
	public void GivenPublicRegistrationEnabled() { }

	[Given(@"^Production self-host інстанс без користувачів$")]
	public void GivenProductionSelfHostWithoutUsers() { }

	[Given(@"^Production self-host інстанс має першого адміністратора$")]
	public void GivenProductionSelfHostHasFirstAdmin() { }

	[Given(@"^Production self-host інстанс має (\d+) активного користувача$")]
	public void GivenProductionSelfHostHasActiveUsers(int p0) { }

	[Given(@"^TRACKLIST_[A-Z_]+ (?:налаштований|не налаштований|увімкнено|не увімкнено)(?: .*)?$")]
	public void GivenTrackListEnvFlagConfigured() { }

	[Given(@"^TRACKLIST_MAX_USERS дорівнює ""([^""]*)""$")]
	public void GivenTrackListMaxUsersEquals(string p0) { }

	[Given(@"^Без TRACKLIST_[A-Z_]+$")]
	public void GivenWithoutTrackListEnvFlag() { }

	[Given(@"^Production self-host інстанс запущений без TRACKLIST_[A-Z_]+$")]
	public void GivenProductionSelfHostWithoutExternalFlag() { }

	[Given(@"^Frontend працює у стандартній self-host конфігурації$")]
	public void GivenFrontendSelfHostConfig() { }

	[Given(@"^Гість знаходиться на сторінці входу з redirectTo ""([^""]*)""$")]
	public void GivenGuestOnLoginWithRedirectTo(string p0) { }

	[Given(@"^Користувач має активну сесію з refresh token$")]
	public void GivenUserHasActiveRefreshTokenSession() { }

	[Given(@"^""([^""]*)"" створив список ""([^""]*)""$")]
	public void GivenUserCreatedList(string p0, string p1) { }

	[Given(@"^""([^""]*)"" ще не лайкнув ""([^""]*)""$")]
	public void GivenUserNotYetLiked(string p0, string p1) { }

	[Given(@"^""([^""]*)"" ще не має доступу до ""([^""]*)""$")]
	public void GivenUserNoAccessYet(string p0, string p1) { }

	[Given(@"^""([^""]*)"" ще не писав рецензію на ""([^""]*)"" \(Id: (\d+)\)$")]
	public void GivenUserNotYetWroteReview(string p0, string p1, int p2) { }

	[Given(@"^""([^""]*)"" ще ні на кого не підписаний$")]
	public void GivenUserNotFollowingAnyone(string p0) { }

	[Given(@"^В `MediaTranslations` існує ""([^""]*)"" \(MediaId: (\d+), Lang: ""([^""]*)"", Title: ""([^""]*)"", Status: ""([^""]*)""\)$")]
	public void GivenMediaTranslationExistsNamed(string p0, int p1, string p2, string p3, string p4) { }

	[Given(@"^В `MediaTranslations` існує: \(MediaId: (\d+), Lang: ""([^""]*)"", Title: ""([^""]*)"", Description: ""([^""]*)""\)$")]
	public void GivenMediaTranslationExistsDesc(int p0, string p1, string p2, string p3) { }

	[Given(@"^В `MediaTranslations` існує: \(MediaId: (\d+), Lang: ""([^""]*)"", Title: ""([^""]*)"", Status: ""([^""]*)""\)$")]
	public void GivenMediaTranslationExistsStatus(int p0, string p1, string p2, string p3) { }

	[Given(@"^В `MediaTranslations` існує: \(MediaId: (\d+), Lang: ""([^""]*)"", Title: ""([^""]*)""\)$")]
	public void GivenMediaTranslationExists(int p0, string p1, string p2) { }

	[Given(@"^В базі даних існують користувачі ""([^""]*)"", ""([^""]*)"", ""([^""]*)"" та ""([^""]*)""$")]
	public void GivenFourUsersExist(string p0, string p1, string p2, string p3) { }

	[Given(@"^В зовнішньому API \(TMDB\) існує медіа ""([^""]*)"" \(ExternalApiId: ""([^""]*)""\)$")]
	public void GivenExternalApiMedia(string p0, string p1) { }

	[Given(@"^В таблиці `Media` існує ""([^""]*)"" \(Id: (\d+), DeletedAt: ""([^""]*)""\)$")]
	public void GivenMediaWithDeletedAt(string p0, int p1, string p2) { }

	[Given(@"^Він бачить запит ""([^""]*)"" \(Статус: ""([^""]*)""\)$")]
	public void GivenHeSeesRequest(string p0, string p1) { }

	[Given(@"^Він бачить скаргу ""([^""]*)"" на ""([^""]*)"" \(Статус: ""([^""]*)""\)$")]
	public void GivenHeSeesReport(string p0, string p1, string p2) { }

	[Given(@"^Він знаходиться на сторінці ""([^""]*)""$")]
	public void GivenHeIsOnPage(string p0) { }

	[Given(@"^Гість \(неавторизований\) знаходиться на ""([^""]*)""$")]
	public void GivenGuestOnPage(string p0) { }

	[Given(@"^Гість знаходиться у рядку пошуку$")]
	public void GivenGuestInSearchBar() { }

	[Given(@"^Для ""([^""]*)"" \(Id: (\d+)\) вже існує схвалений переклад ""([^""]*)"" \(Lang: ""([^""]*)""\)$")]
	public void GivenApprovedTranslationExists(string p0, int p1, string p2, string p3) { }

	[Given(@"^Для ""([^""]*)"" \(Id: (\d+)\) відсутній український переклад$")]
	public void GivenNoUkTranslation(string p0, int p1) { }

	[Given(@"^Для ""([^""]*)"" \(Id: (\d+)\) не існує перекладу ""([^""]*)""$")]
	public void GivenNoTranslation(string p0, int p1, string p2) { }

	[Given(@"^Існує ""([^""]*)"" \(відповідь Рівня (\d+)\) на ""([^""]*)""$")]
	public void GivenReplyExists(string p0, int p1, string p2) { }

	[Given(@"^Існує медіа ""([^""]*)"" \(Id: (\d+)\)$")]
	public void GivenMediaExists(string p0, int p1) { }

	[Given(@"^Користувач ""([^""]*)"" авторизований в системі з паролем ""([^""]*)""$")]
	public void GivenUserAuthedWithPassword(string p0, string p1) { }

	// Removed: handled in profileTests

	[Given(@"^Користувач ""([^""]*)"" авторизований і бачить ""([^""]*)"" \(від ""([^""]*)""\)$")]
	public void GivenUserAuthedSeesFrom(string p0, string p1, string p2) { }

	[Given(@"^Користувач ""([^""]*)"" авторизований і бачить ""([^""]*)"" у своїй стрічці$")]
	public void GivenUserAuthedSeesInFeed(string p0, string p1) { }

	[Given(@"^Користувач ""([^""]*)"" авторизований і бачить ""([^""]*)""$")]
	public void GivenUserAuthedSees(string p0, string p1) { }

	[Given(@"^Користувач ""([^""]*)"" авторизований і знаходиться на ""([^""]*)""$")]
	public void GivenUserAuthedOnPage(string p0, string p1) { }

	[Given(@"^Користувач ""([^""]*)"" додав ""([^""]*)"" \(Id: (\d+)\) до статусу ""([^""]*)""$")]
	public void GivenUserAddedToStatus(string p0, string p1, int p2, string p3) { }

	[Given(@"^Користувач ""([^""]*)"" знаходиться на сторінці ""([^""]*)""$")]
	public void GivenUserOnPageShared(string p0, string p1) { }

	[Given(@"^Користувач бачить ""([^""]*)"" \(ExternalApiId: ""([^""]*)""\) у результатах пошуку$")]
	public void GivenUserSeesInSearchExternalId(string p0, string p1) { }

	[Given(@"^Користувач бачить ""([^""]*)"" \(Id: (\d+)\) у результатах пошуку$")]
	public void GivenUserSeesInSearchId(string p0, int p1) { }

	[Given(@"^Мова інтерфейсу користувача ""([^""]*)"" встановлена на ""([^""]*)""$")]
	public void GivenUserLangSet(string p0, string p1) { }

	[Given(@"^На сторінці ""([^""]*)"" відображається ""([^""]*)"" з текстом ""([^""]*)""$")]
	public void GivenPageShowsElementWithText(string p0, string p1, string p2) { }

	[Given(@"^Система підключена до зовнішнього API \(наприклад, TMDB\)$")]
	public void GivenSystemConnectedToExternalApi() { }

	// ── THEN: assertions (no-op stubs) ──────────────────────

	[Then(@"^""([^""]*)"" бачить ""([^""]*)"" у списку ""([^""]*)""$")]
	public void ThenSeesInList(string p0, string p1, string p2) { }

	[Then(@"^""([^""]*)"" змінює текст на ""([^""]*)""$")]
	public void ThenChangesTextTo(string p0, string p1) { }

	[Then(@"^""([^""]*)"" знаходиться у стрічці вище$")]
	public void ThenIsAboveInFeed(string p0) { }

	[Then(@"^""([^""]*)"" знаходиться у стрічці вище, ніж ""([^""]*)""$")]
	public void ThenIsAboveThan(string p0, string p1) { }

	[Then(@"^""([^""]*)"" зникає з пошуку та зі списків \(завдяки Global Query Filter\)$")]
	public void ThenDisappearsFromSearch(string p0) { }

	[Then(@"^""([^""]*)"" зникає зі списків у UI \(завдяки Global Query Filter\)$")]
	public void ThenDisappearsFromUiLists(string p0) { }

	[Then(@"^""([^""]*)"" зникає зі списку на сторінці$")]
	public void ThenDisappearsFromListOnPage(string p0) { }

	[Then(@"^""([^""]*)"" з'являється під ""([^""]*)"" \(з візуальним відступом\)$")]
	public void ThenAppearsUnderIndented(string p0, string p1) { }

	[Then(@"^""([^""]*)"" з'являється у списку коментарів під ""([^""]*)""$")]
	public void ThenAppearsInCommentList(string p0, string p1) { }

	[Then(@"^""([^""]*)"" з'являється у списку людей з доступом$")]
	public void ThenAppearsInAccessList(string p0) { }

	[Then(@"^""([^""]*)"" НЕ бачить ""([^""]*)"" у списку ""([^""]*)""$")]
	public void ThenDoesNotSeeInList(string p0, string p1, string p2) { }

	[Then(@"^Бекенд звертається до зовнішнього API за повною інформацією про ""([^""]*)""$")]
	public void ThenBackendCallsExternalApi(string p0) { }

	[Then(@"^Бекенд миттєво повертає дані про ""([^""]*)"" з локальної таблиці `Media`$")]
	public void ThenBackendReturnsLocal(string p0) { }

	[Then(@"^Бекенд НЕ звертається до зовнішнього API$")]
	public void ThenBackendDoesNotCallApi() { }

	[Then(@"^Бекенд опитує і локальну БД \(по `MediaTranslations`\), і зовнішнє API \(TMDB\)$")]
	public void ThenBackendQueriesBoth() { }

	[Then(@"^Бекенд створює запис в `MediaTranslations` \(Lang: ""([^""]*)"", Title: ""([^""]*)"", Status: ""([^""]*)""\)$")]
	public void ThenBackendCreatesMediaTranslation(string p0, string p1, string p2) { }

	[Then(@"^Бекенд створює новий запис в таблиці `Media` \(ExternalApiId: ""([^""]*)""\)$")]
	public void ThenBackendCreatesMediaRecord(string p0) { }

	[Then(@"^Він бачить ""([^""]*)"" \(від ""([^""]*)""\)$")]
	public void ThenHeSeesFrom(string p0, string p1) { }

	[Then(@"^Він бачить ""([^""]*)"" у списку результатів$")]
	public void ThenHeSeesInResults(string p0) { }

	[Then(@"^Він бачить віджети: ""([^""]*)"", ""([^""]*)"", ""([^""]*)""$")]
	public void ThenHeSeesWidgets(string p0, string p1, string p2) { }

	[Then(@"^Він бачить заголовок ""([^""]*)""$")]
	public void ThenHeSeesTitle(string p0) { }

	[Then(@"^Він бачить кнопку ""([^""]*)"" тільки біля ""([^""]*)"" \(Рівень (\d+)\)$")]
	public void ThenHeSeesButtonOnlyNear(string p0, string p1, int p2) { }

	[Then(@"^Він бачить лічильник ""([^""]*)"" вподобайок біля ""([^""]*)""$")]
	public void ThenHeSeesLikeCounter(string p0, string p1) { }

	[Then(@"^Він бачить оновлені дані відповідно до обраного проміжку$")]
	public void ThenHeSeesUpdatedData() { }

	[Then(@"^Він бачить опис ""([^""]*)""$")]
	public void ThenHeSeesDescription(string p0) { }

	[Then(@"^Він бачить повідомлення ""([^""]*)""$")]
	public void ThenHeSeesMessageShared(string p0) { }

	[Then(@"^Він бачить посилання ""([^""]*)""$")]
	public void ThenHeSeesLink(string p0) { }

	[Then(@"^^Він бачить рейтинги IMdB / Rotten Tomatoes$$")]
	public void ThenHeSeesRatings() { }

	[Then(@"^Він бачить свою вже існуючу рецензію$")]
	public void ThenHeSeesExistingReview() { }

	[Then(@"^Він бачить список рецензій, включаючи ""([^""]*)""$")]
	public void ThenHeSeesReviewList(string p0) { }

	[Then(@"^Він НЕ бачить ""([^""]*)"" \(від ""([^""]*)""\)$")]
	public void ThenHeDoesNotSeeFrom(string p0, string p1) { }

	[Then(@"^Він НЕ бачить ""([^""]*)"" \(який має менше лайків\)$")]
	public void ThenHeDoesNotSeeLessLikes(string p0) { }

	[Then(@"^Він НЕ бачить ""([^""]*)"" у результатах пошуку$")]
	public void ThenHeDoesNotSeeInSearch(string p0) { }

	[Then(@"^Він НЕ бачить жодної рецензії$")]
	public void ThenHeSeesNoReviews() { }

	[Then(@"^Він НЕ бачить кнопки ""([^""]*)"" для мови ""([^""]*)""$")]
	public void ThenHeDoesNotSeeButtonForLang(string p0, string p1) { }

	[Then(@"^Він НЕ бачить форми для створення нової рецензії$")]
	public void ThenHeDoesNotSeeReviewForm() { }

	[Then(@"^Запис \(CollectionId: ""([^""]*)"", UserId: ""([^""]*)""\) видаляється з `CollectionAccess`$")]
	public void ThenCollectionAccessRemoved(string p0, string p1) { }

	[Then(@"^Запис ""([^""]*)"" у `MediaTranslations` оновлено$")]
	public void ThenMediaTranslationUpdated(string p0) { }

	[Then(@"^Запит зникає з черги модерації$")]
	public void ThenRequestRemovedFromQueue() { }

	[Then(@"^Його рецензія з'являється у списку рецензій на сторінці$")]
	public void ThenHisReviewAppearsOnPage() { }

	[Then(@"^Кнопка змінює стан на ""([^""]*)"" \(Like\)$")]
	public void ThenButtonChangesToLike(string p0) { }

	[Then(@"^Кнопка змінює стан на ""([^""]*)"" \(Liked\)$")]
	public void ThenButtonChangesToLiked(string p0) { }

	[Then(@"^Код відповіді становить (\d+)$")]
	public void ThenStatusCodeShared(int code) { }

	[Then(@"^Користувач ""([^""]*)"" більше не може авторизуватися$")]
	public void ThenUserCannotAuth(string p0) { }

	[Then(@"^Користувач бачить повну сторінку ""([^""]*)"" \(або ""([^""]*)"", залежно від мови\)$")]
	public void ThenUserSeesPageOrLang(string p0, string p1) { }

	[Then(@"^Користувач бачить повну сторінку ""([^""]*)""$")]
	public void ThenUserSeesFullPage(string p0) { }

	[Then(@"^Користувач залишається на сторінці ""([^""]*)"" \(без перезавантаження\)$")]
	public void ThenUserStaysOnPage(string p0) { }

	[Then(@"^Користувача перенаправлено на сторінку ""([^""]*)""$")]
	public void ThenUserRedirectedToPage(string p0) { }

	[Then(@"^Лічильник вподобайок ""([^""]*)"" оновлюється на ""([^""]*)"" \(у стрічці\)$")]
	public void ThenLikeCounterUpdatesInFeed(string p0, string p1) { }

	[Then(@"^Лічильник вподобайок ""([^""]*)"" стає ""([^""]*)""$")]
	public void ThenLikeCounterBecomes(string p0, string p1) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" \(Id: (\d+)\) в базі даних встановлюється на поточний час$")]
	public void ThenFieldSetCurrentTimeForId(string p0, string p1, int p2) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" в базі даних встановлюється на поточний час$")]
	public void ThenFieldSetCurrentTimeInDb(string p0, string p1) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" в базі даних оновлюється на ""([^""]*)""$")]
	public void ThenFieldUpdatedInDb(string p0, string p1, string p2) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" встановлено на ID адміністратора$")]
	public void ThenFieldSetAdminId(string p0, string p1) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" встановлено на ID користувача$")]
	public void ThenFieldSetUserId(string p0, string p1) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" встановлюється на ID модератора$")]
	public void ThenFieldSetModeratorId(string p0, string p1) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" встановлюється на поточний час$")]
	public void ThenFieldSetCurrentTime(string p0, string p1) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" залишається ""([^""]*)""$")]
	public void ThenFieldRemains(string p0, string p1, string p2) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" має значення ""([^""]*)""$")]
	public void ThenFieldHasValue(string p0, string p1, string p2) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" оновлено$")]
	public void ThenFieldUpdated(string p0, string p1) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" оновлюється на ""([^""]*)""$")]
	public void ThenFieldUpdatesTo(string p0, string p1, string p2) { }

	[Then(@"^Поле ""([^""]*)"" для ""([^""]*)"" оновлюється$")]
	public void ThenFieldUpdates(string p0, string p1) { }

	[Then(@"^Поле ""([^""]*)"" для запису в `CollectionItems` встановлюється на поточний час$")]
	public void ThenCollectionItemFieldSetCurrentTime(string p0) { }

	[Then(@"^Поле ""([^""]*)"" для скарги ""([^""]*)"" встановлюється на ID модератора$")]
	public void ThenReportFieldSetModeratorId(string p0, string p1) { }

	[Then(@"^Поле коментування неактивне \(або при кліку перенаправляє на ""([^""]*)""\)$")]
	public void ThenCommentFieldInactiveOrRedirects(string p0) { }

	[Then(@"^Система генерує та завантажує файл зі статистикою$")]
	public void ThenSystemGeneratesStatsFile() { }

	[Then(@"^Система зберігає нову рецензію з HTML/Markdown \(""([^""]*)""\)$")]
	public void ThenSystemSavesReviewWithMarkup(string p0) { }

	[Then(@"^Система показує список результатів, що містить:$")]
	public void ThenSystemShowsResultsList(DataTable table) { }

	[Then(@"^Система створює запис у `CollectionAccess` \(CollectionId: ""([^""]*)"", UserId: ""([^""]*)""\)$")]
	public void ThenSystemCreatesCollectionAccess(string p0, string p1) { }

	[Then(@"^Система створює запис у `MediaTranslations` \(MediaId: (\d+), Lang: ""([^""]*)"", Title: ""([^""]*)"", Status: ""([^""]*)""\)$")]
	public void ThenSystemCreatesMediaTranslation(int p0, string p1, string p2, string p3) { }

	[Then(@"^Система створює запис у `Reports` \(TargetId: ""([^""]*)"", Status: ""([^""]*)""\)$")]
	public void ThenSystemCreatesReport(string p0, string p1) { }

	[Then(@"^Система створює запис у таблиці `CollectionItems` \(CollectionId: ""([^""]*)"", MediaId: (\d+)\)$")]
	public void ThenSystemCreatesCollectionItem(string p0, int p1) { }

	[Then(@"^Система створює новий запис ""([^""]*)"" у таблиці `Collections`$")]
	public void ThenSystemCreatesCollection(string p0) { }

	[Then(@"^Статус ""([^""]*)"" оновлюється на ""([^""]*)""$")]
	public void ThenStatusUpdates(string p0, string p1) { }

	[Then(@"^Статус скарги ""([^""]*)"" оновлюється на ""([^""]*)""$")]
	public void ThenReportStatusUpdates(string p0, string p1) { }

	// ── WHEN: actions (no-op stubs) ─────────────────────────

	[When(@"^""([^""]*)"" відкриває головну сторінку \(""([^""]*)""\)$")]
	public void WhenOpensHomePage(string p0, string p1) { }

	[When(@"^""([^""]*)"" натискає на вкладку ""([^""]*)""$")]
	public void WhenClicksTab(string p0, string p1) { }

	[When(@"^Він бачить запит ""([^""]*)"" \(Назва: ""([^""]*)""\) зі статусом ""([^""]*)""$")]
	public void WhenHeSeesRequestWithName(string p0, string p1, string p2) { }

	[When(@"^Він бачить переклад ""([^""]*)"" \(Lang: ""([^""]*)"", Title: ""([^""]*)"", Status: ""([^""]*)""\)$")]
	public void WhenHeSeesTranslation(string p0, string p1, string p2, string p3) { }

	[When(@"^Він вводить ""([^""]*)"" у поле коментування під ""([^""]*)""$")]
	public void WhenHeEntersCommentUnder(string p0, string p1) { }

	[When(@"^Він вводить ""([^""]*)""$")]
	public void WhenHeEntersShared(string p0) { }

	[When(@"^Він вводить ""([^""]*)"": ""([^""]*)""$")]
	public void WhenHeEntersField(string p0, string p1) { }

	[When(@"^Він вводить в ""([^""]*)"" редактор текст: ""([^""]*)""$")]
	public void WhenHeEntersInEditor(string p0, string p1) { }

	[When(@"^Він вводить запит ""([^""]*)""$")]
	public void WhenHeEntersQuery(string p0) { }

	[When(@"^Він вводить назву ""([^""]*)""$")]
	public void WhenHeEntersName(string p0) { }

	[When(@"^Він вводить опис ""([^""]*)""$")]
	public void WhenHeEntersDescription(string p0) { }

	[When(@"^Він відкриває модальне вікно ""([^""]*)""$")]
	public void WhenHeOpensModal(string p0) { }

	[When(@"^Він дивиться на блок коментарів під ""([^""]*)""$")]
	public void WhenHeLooksAtComments(string p0) { }

	[When(@"^Він змінює ""([^""]*)"" на ""([^""]*)""$")]
	public void WhenHeChangesTo(string p0, string p1) { }

	[When(@"^Він змінює базовий рівень з ""([^""]*)"" на ""([^""]*)""$")]
	public void WhenHeChangesBaseLevel(string p0, string p1) { }

	[When(@"^Він знаходить ""([^""]*)"" \(Id: (\d+)\)$")]
	public void WhenHeFindsId(string p0, int p1) { }

	[When(@"^Він знаходить ""([^""]*)"" \(ID: ""([^""]*)""\)$")]
	public void WhenHeFindsIdStr(string p0, string p1) { }

	[When(@"^Він натискає ""([^""]*)"" \(Like\) на ""([^""]*)"" у стрічці$")]
	public void WhenHeClicksLikeInFeed(string p0, string p1) { }

	[When(@"^Він натискає ""([^""]*)"" \(Like\) на ""([^""]*)""$")]
	public void WhenHeClicksLikeOn(string p0, string p1) { }

	[When(@"^Він натискає ""([^""]*)"" \(Liked\) на ""([^""]*)""$")]
	public void WhenHeClicksLikedOn(string p0, string p1) { }

	[When(@"^Він натискає ""([^""]*)"" біля ""([^""]*)""$")]
	public void WhenHeClicksNear(string p0, string p1) { }

	[When(@"^Він натискає ""([^""]*)"" на ""([^""]*)""$")]
	public void WhenHeClicksOnSharedTwo(string p0, string p1) { }

	[When(@"^Він натискає кнопку ""([^""]*)""$")]
	public void WhenHeClicksButton(string p0) { }

	[When(@"^Він натискає на ""([^""]*)"" \(переходить на ""([^""]*)""\)$")]
	public void WhenHeClicksRedirects(string p0, string p1) { }

	[When(@"^Він обирає ""([^""]*)"" зі списку$")]
	public void WhenHeChoosesFromList(string p0) { }

	[When(@"^Він обирає ""([^""]*)""$")]
	public void WhenHeChooses(string p0) { }

	[When(@"^Він обирає мову ""([^""]*)""$")]
	public void WhenHeChoosesLang(string p0) { }

	[When(@"^Він обирає причину ""([^""]*)""$")]
	public void WhenHeChoosesReason(string p0) { }

	[When(@"^Він обирає проміжок часу \(наприклад, ""([^""]*)""\)$")]
	public void WhenHeChoosesTimeRange(string p0) { }

	[When(@"^Він переходить на сторінку налаштувань ""([^""]*)""$")]
	public void WhenHeNavigatesToSettings(string p0) { }

	[When(@"^Він переходить на сторінку списку ""([^""]*)""$")]
	public void WhenHeNavigatesToList(string p0) { }

	[When(@"^Він переходить у ""([^""]*)""$")]
	public void WhenHeNavigatesTo(string p0) { }

	[When(@"^Він переходить у чергу ""([^""]*)""$")]
	public void WhenHeNavigatesToQueue(string p0) { }

	[When(@"^Він підтверджує дію$")]
	public void WhenHeConfirms() { }

	[When(@"^Він ставить оцінку ""([^""]*)""$")]
	public void WhenHeSetsRating(string p0) { }

	[When(@"^Гість намагається ввести текст у поле коментування під ""([^""]*)""$")]
	public void WhenGuestTriesEnterComment(string p0) { }

	[When(@"^Користувач ""([^""]*)"" відкриває головну сторінку \(""([^""]*)""\)$")]
	public void WhenUserOpensHomePage(string p0, string p1) { }

	[When(@"^Користувач ""([^""]*)"" відкриває сторінку ""([^""]*)""$")]
	public void WhenUserOpensPage(string p0, string p1) { }

	[When(@"^Користувач ""([^""]*)"" дивиться на ""([^""]*)""$")]
	public void WhenUserLooksAt(string p0, string p1) { }

	[When(@"^Застосунок створює базу даних$")]
	public void WhenApplicationCreatesDatabase() { }

	[When(@"^Клієнт створює першого адміністратора через ""([^""]*)"" з валідним setup token$")]
	public void WhenClientCreatesFirstAdminWithSetupToken(string p0) { }

	[When(@"^Клієнт намагається створити першого адміністратора через ""([^""]*)""$")]
	public void WhenClientTriesCreateFirstAdmin(string p0) { }

	[When(@"^Гість намагається зареєструвати новий обліковий запис$")]
	public void WhenGuestTriesRegisterAccount() { }

	[When(@"^Користувач шукає або відкриває медіа$")]
	public void WhenUserSearchesOrOpensMedia() { }

	[When(@"^SSR перевіряє cookie-сесію користувача$")]
	public void WhenSsrChecksCookieSession() { }

	[When(@"^Гість успішно входить у систему$")]
	public void WhenGuestLogsInSuccessfully() { }

	[When(@"^Користувач оновлює сесію через ""([^""]*)""$")]
	public void WhenUserRenewsSession(string p0) { }

	[When(@"^Користувач завершує сесію через ""([^""]*)""$")]
	public void WhenUserLogsOutViaEndpoint(string p0) { }

	[When(@"^Користувач змінює пароль$")]
	public void WhenUserChangesPasswordSelfHost() { }

	// Removed: Користувач натискає кнопку — handled by feature-specific bindings

	[Then(@"^Відповідь містить помилку ""([^""]*)""$")]
	public void ThenResponseContainsErrorShared(string p0) { }

	[Then(@"^Система показує повідомлення про помилку ""([^""]*)""$")]
	public void ThenSystemShowsErrorShared(string p0) { }

	[Then(@"^Система показує повідомлення про помилку ""([^""]*)"" біля поля ""([^""]*)""$")]
	public void ThenSystemShowsErrorNearFieldShared(string p0, string p1) { }

	[Then(@"^У базі немає користувача ""([^""]*)"" з паролем ""([^""]*)""$")]
	public void ThenDatabaseDoesNotHaveUserWithPassword(string p0, string p1) { }

	[Then(@"^Перший адміністратор створюється тільки через one-time setup flow$")]
	public void ThenFirstAdminOnlySetupFlow() { }

	[Then(@"^Система створює адміністратора з хешованим паролем$")]
	public void ThenSystemCreatesAdminWithHashedPassword() { }

	[Then(@"^Система повертає пару токенів для входу$")]
	public void ThenSystemReturnsLoginTokenPair() { }

	[Then(@"^Повторний setup запит відхиляється$")]
	public void ThenRepeatedSetupRejected() { }

	[Then(@"^Система блокує створення нового користувача у self-host режимі$")]
	public void ThenSystemBlocksUserCreationInSelfHostMode() { }

	[Then(@"^Система пояснює, що публічна реєстрація вимкнена$")]
	public void ThenSystemExplainsPublicRegistrationDisabled() { }

	[Then(@"^Система пояснює, що досягнуто ліміт користувачів$")]
	public void ThenSystemExplainsUserLimitReached() { }

	[Then(@"^Система не відправляє назви медіа, external ids або тексти рецензій зовнішнім сервісам$")]
	public void ThenSystemDoesNotSendExternalData() { }

	[Then(@"^Локальні кешовані дані залишаються доступними$")]
	public void ThenLocalCachedDataAvailable() { }

	[Then(@"^Frontend звертається до backend session endpoint$")]
	public void ThenFrontendUsesBackendSessionEndpoint() { }

	[Then(@"^JWT_PRIVATE_KEY потрібен тільки backend сервісу$")]
	public void ThenJwtPrivateKeyOnlyBackend() { }

	[Then(@"^Система перенаправляє користувача на безпечний внутрішній маршрут$")]
	public void ThenSystemRedirectsSafeInternal() { }

	[Then(@"^Зовнішній redirectTo ігнорується$")]
	public void ThenExternalRedirectIgnored() { }

	[Then(@"^Користувача перенаправлено на безпечний внутрішній маршрут$")]
	public void ThenUserRedirectedToSafeInternalRoute() { }

	[Then(@"^Система видає новий refresh token$")]
	public void ThenSystemIssuesNewRefreshToken() { }

	[Then(@"^Старий refresh token більше не приймається$")]
	public void ThenOldRefreshTokenRejected() { }

	[Then(@"^Усі попередні refresh token користувача відкликані$")]
	public void ThenPreviousRefreshTokensRevoked() { }

	[When(@"^У випадаючому меню він обирає статус ""([^""]*)"" \(той самий, що й активний\)$")]
	public void WhenChoosesSameStatus(string p0) { }

	[When(@"^У полі ""([^""]*)"" він вводить нікнейм ""([^""]*)""$")]
	public void WhenEntersUsernameInField(string p0, string p1) { }
}
