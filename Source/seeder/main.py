"""Orchestrator. Run phases in sequence. Each phase is idempotent."""
import json
import logging
import random
import re
import sys
import time
from collections import defaultdict
from pathlib import Path

# Make seeder/ root importable when running `py main.py`
sys.path.insert(0, str(Path(__file__).parent))

from config import (  # noqa: E402
    API_BASE, TMDB_API_KEY, DATA_DIR, SCRAPED_DIR, LOG_FILE,
    TARGET_MEDIA_COUNT, TARGET_LONG_REVIEWS, TARGET_SHORT_REVIEWS,
    TARGET_USERS, PER_MEDIA_REVIEW_CAP,
    LONG_REVIEW_MIN_CHARS, SHORT_REVIEW_MAX_CHARS,
    SUBREDDITS, MOVIE_GENRES, TV_GENRES, DISCOVER_YEARS,
    SEED_PASSWORD,
)
from state import State  # noqa: E402
from api_client import ApiClient  # noqa: E402
from tmdb_client import TmdbClient  # noqa: E402
from reddit_scraper import RedditScraper  # noqa: E402
from text_utils import (  # noqa: E402
    slugify_username, make_email, extract_rating, sentiment_rating,
    clean_review_text, extract_media_title,
)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[
        logging.FileHandler(str(LOG_FILE), encoding="utf-8"),
        logging.StreamHandler(sys.stdout),
    ],
)
log = logging.getLogger("seed")

random.seed(42)


# ============================================================
# Phase 1: seed media from TMDB
# ============================================================
def phase_seed_media(api: ApiClient, tmdb: TmdbClient, media_state: State) -> None:
    log.info("=== Phase 1: seed media ===")

    target = TARGET_MEDIA_COUNT
    done_count = len(media_state.data)
    if done_count >= target:
        log.info(f"already have {done_count} media — skip")
        return

    # Combine movie + TV from discover across genres + years
    candidates: list[tuple[str, int]] = []  # (type, tmdb_id)
    seen = set()

    # Movies: 1-2 pages per genre+year combo
    for genre in MOVIE_GENRES:
        for year in DISCOVER_YEARS:
            for page in (1,):
                results = tmdb.discover("movie", genre_id=genre, year=year, page=page)
                for r in results[:5]:  # top 5 per combo
                    tid = r.get("id")
                    if tid and tid not in seen:
                        seen.add(tid)
                        candidates.append(("movie", tid))
                if len(candidates) >= target * 1.5:
                    break
            if len(candidates) >= target * 1.5:
                break
        if len(candidates) >= target * 1.5:
            break

    # TV: fewer needed
    tv_seen = set()
    for genre in TV_GENRES[:8]:
        for year in DISCOVER_YEARS[:5]:
            results = tmdb.discover("tv", genre_id=genre, year=year, page=1)
            for r in results[:3]:
                tid = r.get("id")
                if tid and tid not in tv_seen:
                    tv_seen.add(tid)
                    candidates.append(("tv", tid))
            if len([c for c in candidates if c[0] == "tv"]) >= target // 3:
                break
        if len([c for c in candidates if c[0] == "tv"]) >= target // 3:
            break

    random.shuffle(candidates)
    log.info(f"discovered {len(candidates)} candidates; target {target}")

    inserted = 0
    for (mtype, tid) in candidates:
        if inserted + done_count >= target:
            break
        key = f"Tmdb:{mtype}:{tid}"
        if media_state.has(key):
            continue
        media = api.get_media_by_external_id(key)
        if not media:
            log.warning(f"failed to import {key}")
            continue
        title = None
        translations = media.get("translations") or []
        if translations:
            title = translations[0].get("title")
        media_state.set(key, {
            "guid": media["id"],
            "type": mtype,
            "title": title,
            "year": media.get("releaseYear"),
        })
        inserted += 1
        if inserted % 20 == 0:
            media_state.save()
            log.info(f"imported {inserted} media so far ({inserted + done_count}/{target})")

    media_state.save()
    log.info(f"=== Phase 1 done: {len(media_state.data)} media total ===")


# ============================================================
# Phase 2: pull TMDB reviews per seeded media
# ============================================================
def phase_pull_tmdb_reviews(tmdb: TmdbClient, media_state: State, raw_reviews_state: State) -> None:
    log.info("=== Phase 2: pull TMDB reviews ===")

    reviews: list[dict] = raw_reviews_state.get("items", [])
    seen_keys = set(r.get("dedupe_key") for r in reviews)
    seen_media_pulled = set(raw_reviews_state.get("tmdb_pulled", []))

    new_count = 0
    media_items = list(media_state.data.items())
    for i, (ext_id, info) in enumerate(media_items):
        if ext_id in seen_media_pulled:
            continue
        mtype = info["type"]
        try:
            tmdb_id = int(ext_id.split(":")[2])
        except Exception:
            continue
        for r in tmdb.reviews(mtype, tmdb_id):
            author = (r.get("author_details") or {}).get("username") or r.get("author") or ""
            content = clean_review_text(r.get("content") or "")
            if not author or not content:
                continue
            rating = (r.get("author_details") or {}).get("rating")
            if rating is None:
                rating = extract_rating(content)
            if rating is None:
                rating = sentiment_rating(content)
            rating = max(1, min(10, int(round(float(rating)))))
            dkey = f"tmdb|{author}|{ext_id}"
            if dkey in seen_keys:
                continue
            seen_keys.add(dkey)
            reviews.append({
                "source": "tmdb",
                "author": author,
                "content": content,
                "rating": rating,
                "external_id": ext_id,
                "dedupe_key": dkey,
            })
            new_count += 1
        seen_media_pulled.add(ext_id)
        if i % 25 == 0:
            raw_reviews_state.set("items", reviews)
            raw_reviews_state.set("tmdb_pulled", list(seen_media_pulled))
            raw_reviews_state.save()
            log.info(f"tmdb reviews: {len(reviews)} total ({new_count} new this run); media scanned {i+1}/{len(media_items)}")

    raw_reviews_state.set("items", reviews)
    raw_reviews_state.set("tmdb_pulled", list(seen_media_pulled))
    raw_reviews_state.save()
    log.info(f"=== Phase 2 done: TMDB total {len([r for r in reviews if r['source']=='tmdb'])} ===")


# ============================================================
# Phase 3: scrape Reddit
# ============================================================
def phase_scrape_reddit(scraper: RedditScraper, tmdb: TmdbClient, media_state: State, raw_reviews_state: State) -> None:
    log.info("=== Phase 3: scrape Reddit ===")

    reviews: list[dict] = raw_reviews_state.get("items", [])
    seen_keys = set(r.get("dedupe_key") for r in reviews)
    subs_done = set(raw_reviews_state.get("reddit_subs_done", []))

    # Build a title→external_id map so we can match Reddit thread titles → media
    title_to_ext: dict[str, str] = {}
    for ext_id, info in media_state.data.items():
        if info.get("title"):
            title_to_ext[info["title"].lower().strip()] = ext_id

    # For TMDB-search fallback (when title not in seeded media)
    extra_media_added = 0
    api_for_import = ApiClient()

    def ensure_media_for_title(title: str, hint_type: str = "movie") -> str | None:
        """Find a TMDB id for title; if backend doesn't have it yet, auto-import."""
        tl = title.lower().strip()
        if tl in title_to_ext:
            return title_to_ext[tl]
        # search TMDB
        results = tmdb.search_movie(title) if hint_type == "movie" else tmdb.search_tv(title)
        if not results:
            results = tmdb.search_multi(title)
        if not results:
            return None
        top = results[0]
        rtype = top.get("media_type") or hint_type
        if rtype not in ("movie", "tv"):
            return None
        tid = top.get("id")
        if not tid:
            return None
        ext_id = f"Tmdb:{rtype}:{tid}"
        if ext_id in media_state.data:
            title_to_ext[tl] = ext_id
            return ext_id
        # import to DB
        media = api_for_import.get_media_by_external_id(ext_id)
        if not media:
            return None
        t = None
        translations = media.get("translations") or []
        if translations:
            t = translations[0].get("title")
        media_state.set(ext_id, {
            "guid": media["id"],
            "type": rtype,
            "title": t,
            "year": media.get("releaseYear"),
        })
        nonlocal extra_media_added
        extra_media_added += 1
        title_to_ext[tl] = ext_id
        if t:
            title_to_ext[t.lower().strip()] = ext_id
        return ext_id

    for sub in SUBREDDITS:
        if sub in subs_done:
            continue
        log.info(f"reddit: scraping r/{sub}")
        # Pull top of all time + top year for variety
        posts = scraper.top_posts(sub, t="all", limit=100) + scraper.top_posts(sub, t="year", limit=100)
        # dedupe posts by id
        seen_pids = set()
        unique_posts = []
        for p in posts:
            pid = p.get("id")
            if pid and pid not in seen_pids:
                seen_pids.add(pid)
                unique_posts.append(p)

        for p in unique_posts:
            # 1. selftext on post counts as a "long review" if review-shaped
            title = p.get("title") or ""
            selftext = p.get("selftext") or ""
            author = p.get("author") or ""
            pid = p.get("id")
            tl_lower = title.lower()
            review_thread = any(k in tl_lower for k in ("review", "official discussion", "discussion thread"))

            media_title = extract_media_title(title)
            ext_id = None
            if media_title and review_thread:
                hint = "tv" if sub in ("television", "TelevisionReviews") else "movie"
                ext_id = ensure_media_for_title(media_title, hint)

            # post-body as review if long enough & has author
            if ext_id and author and author != "[deleted]" and len(selftext) > 200:
                content = clean_review_text(selftext)
                if content:
                    rating = extract_rating(content) or sentiment_rating(content)
                    dkey = f"reddit-p|{author}|{ext_id}"
                    if dkey not in seen_keys:
                        seen_keys.add(dkey)
                        reviews.append({
                            "source": "reddit-post",
                            "author": author,
                            "content": content,
                            "rating": rating,
                            "external_id": ext_id,
                            "dedupe_key": dkey,
                        })

            # 2. comments — only if it's an "official discussion" / review thread
            if ext_id and review_thread and pid:
                for c in scraper.comments(sub, pid, limit=200):
                    cauthor = c.get("author") or ""
                    cbody = clean_review_text(c.get("body") or "")
                    if not cauthor or cauthor == "[deleted]" or len(cbody) < 30:
                        continue
                    rating = extract_rating(cbody) or sentiment_rating(cbody)
                    dkey = f"reddit-c|{cauthor}|{ext_id}|{pid}|{c.get('id','')}"
                    if dkey in seen_keys:
                        continue
                    seen_keys.add(dkey)
                    reviews.append({
                        "source": "reddit-comment",
                        "author": cauthor,
                        "content": cbody,
                        "rating": rating,
                        "external_id": ext_id,
                        "dedupe_key": dkey,
                    })
        subs_done.add(sub)
        raw_reviews_state.set("items", reviews)
        raw_reviews_state.set("reddit_subs_done", list(subs_done))
        raw_reviews_state.save()
        log.info(f"r/{sub} done. total reviews so far: {len(reviews)}; extra media added: {extra_media_added}")

    log.info(f"=== Phase 3 done: total {len(reviews)} raw reviews, {len(media_state.data)} media ===")
    media_state.save()


# ============================================================
# Phase 4: register users
# ============================================================
def phase_register_users(api: ApiClient, raw_reviews_state: State, users_state: State) -> None:
    log.info("=== Phase 4: register users ===")

    reviews: list[dict] = raw_reviews_state.get("items", [])

    # Count reviews per author — register most prolific first (max value per BCrypt cost)
    counts: dict[str, int] = defaultdict(int)
    for r in reviews:
        counts[r["author"]] += 1

    ranked = sorted(counts.items(), key=lambda x: x[1], reverse=True)

    target_users = max(TARGET_USERS, 100)
    new_count = 0
    for author, _cnt in ranked:
        if len(users_state.data) >= target_users:
            break
        if author in users_state.data:
            continue
        username = slugify_username(author)
        if not username or len(username) < 3:
            continue
        # collision: if username already used by different author
        if any(u.get("username") == username for u in users_state.data.values()):
            continue
        email = make_email(username)
        res = api.register(email, username)
        if not res:
            log.warning(f"register failed for {author} → {username}")
            continue
        users_state.set(author, {
            "username": username,
            "email": email,
            "token": res["accessToken"],
            "refresh": res["refreshToken"],
            "avatar_url": f"https://api.dicebear.com/7.x/avataaars/svg?seed={username}",
        })
        new_count += 1
        if new_count % 10 == 0:
            users_state.save()
            log.info(f"registered {new_count} users (total {len(users_state.data)})")

    users_state.save()
    log.info(f"=== Phase 4 done: {len(users_state.data)} users registered ===")

    # Try to set DiceBear avatars (best effort)
    log.info("setting avatar URLs...")
    set_count = 0
    for handle, info in users_state.data.items():
        if info.get("avatar_set"):
            continue
        profile = api.get_user_profile(info["username"])
        if not profile:
            continue
        if api.set_avatar_url(info["token"], profile, info["avatar_url"]):
            info["avatar_set"] = True
            set_count += 1
            if set_count % 20 == 0:
                users_state.save()
                log.info(f"avatars set: {set_count}")
    users_state.save()
    log.info(f"avatars set total: {set_count}/{len(users_state.data)}")


# ============================================================
# Phase 5: post reviews
# ============================================================
def phase_post_reviews(api: ApiClient, raw_reviews_state: State, media_state: State, users_state: State, posted_state: State) -> None:
    log.info("=== Phase 5: post reviews ===")

    reviews: list[dict] = raw_reviews_state.get("items", [])
    posted = set(posted_state.get("keys", []))
    stats = posted_state.get("stats", {"long": 0, "short": 0})

    # Group by author → only those with registered user
    by_author: dict[str, list[dict]] = defaultdict(list)
    for r in reviews:
        if r["author"] in users_state.data:
            by_author[r["author"]].append(r)

    # Track per-media review count for cap
    per_media_count: dict[str, int] = defaultdict(int)
    for k in posted:
        try:
            _uid, mid = k.split("|")
            per_media_count[mid] += 1
        except Exception:
            pass

    # Split into long pool + short pool. Process in two passes (long first — they're rarer).
    long_pool = []
    short_pool = []
    for author, items in by_author.items():
        for r in items:
            content = r["content"]
            if content and len(content) >= LONG_REVIEW_MIN_CHARS:
                long_pool.append(r)
            else:
                short_pool.append(r)

    # Shuffle within pools so distribution looks natural
    random.shuffle(long_pool)
    random.shuffle(short_pool)

    log.info(f"pools: long={len(long_pool)}, short={len(short_pool)}")
    log.info(f"already posted: {len(posted)}; targets: long {TARGET_LONG_REVIEWS}, short {TARGET_SHORT_REVIEWS}")

    def post_pool(pool: list[dict], pool_name: str, target: int):
        nonlocal posted, stats
        n = 0
        for r in pool:
            if stats[pool_name] >= target:
                break
            author = r["author"]
            ext_id = r["external_id"]
            media_info = media_state.data.get(ext_id)
            if not media_info:
                continue
            mid = media_info["guid"]
            user_info = users_state.data.get(author)
            if not user_info:
                continue
            uid_key = user_info["username"]
            key = f"{uid_key}|{mid}"
            if key in posted:
                continue
            if per_media_count[mid] >= PER_MEDIA_REVIEW_CAP:
                continue
            token = user_info["token"]
            res = api.post_review(token, mid, r["rating"], r["content"])
            if res is None:
                # Maybe token expired — try re-login
                login = api.login(user_info["email"], user_info["username"], SEED_PASSWORD)
                if login:
                    token = login["accessToken"]
                    user_info["token"] = token
                    users_state.save()
                    res = api.post_review(token, mid, r["rating"], r["content"])
            if res is None:
                # Most likely: duplicate review (BRL-4). Mark posted to skip retry.
                posted.add(key)
                per_media_count[mid] += 1
                continue
            posted.add(key)
            per_media_count[mid] += 1
            stats[pool_name] += 1
            n += 1
            if n % 50 == 0:
                posted_state.set("keys", list(posted))
                posted_state.set("stats", stats)
                posted_state.save()
                log.info(f"[{pool_name}] posted {n} this run (total {pool_name}: {stats[pool_name]})")

        posted_state.set("keys", list(posted))
        posted_state.set("stats", stats)
        posted_state.save()
        log.info(f"[{pool_name}] done — posted {n} this run; total {stats[pool_name]}/{target}")

    post_pool(long_pool, "long", TARGET_LONG_REVIEWS)

    # For short pool: include rating-only (empty content) variants
    # If we don't have enough short reviews, synthesize rating-only from users with no remaining short content
    if stats["short"] < TARGET_SHORT_REVIEWS:
        # Generate rating-only entries: pick (user, media) combos not yet posted
        synth = []
        author_list = list(users_state.data.keys())
        media_list = list(media_state.data.keys())
        for author in author_list:
            user_info = users_state.data.get(author)
            if not user_info:
                continue
            uid_key = user_info["username"]
            # Pick up to N random media not yet reviewed
            random.shuffle(media_list)
            for ext_id in media_list:
                if stats["short"] + len(synth) + len(short_pool) >= TARGET_SHORT_REVIEWS * 1.3:
                    break
                media_info = media_state.data[ext_id]
                mid = media_info["guid"]
                key = f"{uid_key}|{mid}"
                if key in posted:
                    continue
                if per_media_count[mid] >= PER_MEDIA_REVIEW_CAP:
                    continue
                # Rating distribution: skewed positive (6-9 most common)
                r = random.choices([3, 4, 5, 6, 7, 8, 9, 10], weights=[2, 3, 5, 10, 18, 25, 22, 15])[0]
                # Short text variants
                short_text_choices = [
                    "", "", "",  # often empty
                    f"{r}/10", "Solid.", "Pretty good.", "Not bad.", "Loved it.", "Meh.",
                    "Decent watch.", "Worth it.", "Mid.", "Skip.", "Hidden gem.",
                ]
                content = random.choice(short_text_choices)
                synth.append({
                    "author": author,
                    "external_id": ext_id,
                    "rating": r,
                    "content": content,
                    "source": "synth-short",
                    "dedupe_key": f"synth|{author}|{ext_id}",
                })
            if stats["short"] + len(synth) >= TARGET_SHORT_REVIEWS * 1.3:
                break

        random.shuffle(synth)
        log.info(f"synthesized {len(synth)} short/empty rating-only entries to fill gap")
        post_pool(synth + short_pool, "short", TARGET_SHORT_REVIEWS)
    else:
        post_pool(short_pool, "short", TARGET_SHORT_REVIEWS)

    log.info(f"=== Phase 5 done: long {stats['long']}, short {stats['short']} ===")


# ============================================================
# Main
# ============================================================
def main():
    phase_arg = sys.argv[1] if len(sys.argv) > 1 else "all"

    api = ApiClient()
    tmdb = TmdbClient()
    scraper = RedditScraper()

    media_state = State("media")
    raw_reviews_state = State("raw_reviews")
    users_state = State("users")
    posted_state = State("posted")

    log.info(f"seeder start — API={API_BASE}, phase={phase_arg}")
    # Health check
    admin_token, admin_error = api.ensure_admin()
    if not admin_token:
        log.error(f"admin bootstrap failed — {admin_error}")
        sys.exit(1)
    log.info("admin login OK")

    phases = phase_arg.split(",") if phase_arg != "all" else ["1", "2", "3", "4", "5"]

    if "1" in phases:
        phase_seed_media(api, tmdb, media_state)
    if "2" in phases:
        phase_pull_tmdb_reviews(tmdb, media_state, raw_reviews_state)
    if "3" in phases:
        phase_scrape_reddit(scraper, tmdb, media_state, raw_reviews_state)
    if "4" in phases:
        phase_register_users(api, raw_reviews_state, users_state)
    if "5" in phases:
        phase_post_reviews(api, raw_reviews_state, media_state, users_state, posted_state)

    log.info("seeder done")


if __name__ == "__main__":
    main()
