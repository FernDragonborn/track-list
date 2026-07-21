"""One-off: post any remaining real (non-synth) reviews not yet posted."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from state import State  # noqa: E402
from api_client import ApiClient  # noqa: E402
from config import PER_MEDIA_REVIEW_CAP, SEED_PASSWORD  # noqa: E402
from collections import defaultdict


def main():
    api = ApiClient()
    media_state = State("media")
    raw_reviews_state = State("raw_reviews")
    users_state = State("users")
    posted_state = State("posted")

    reviews: list[dict] = raw_reviews_state.get("items", [])
    posted = set(posted_state.get("keys", []))

    # Track per-media count from posted set
    per_media_count: dict[str, int] = defaultdict(int)
    for k in posted:
        try:
            _, mid = k.split("|")
            per_media_count[mid] += 1
        except Exception:
            pass

    # Filter: only TMDB-source reviews with author in users_state and not yet posted
    candidates = []
    for r in reviews:
        author = r["author"]
        ext_id = r["external_id"]
        if author not in users_state.data:
            continue
        if ext_id not in media_state.data:
            continue
        mid = media_state.data[ext_id]["guid"]
        uid_key = users_state.data[author]["username"]
        key = f"{uid_key}|{mid}"
        if key in posted:
            continue
        if per_media_count[mid] >= PER_MEDIA_REVIEW_CAP:
            continue
        candidates.append((r, mid, uid_key))

    print(f"candidates: {len(candidates)}")

    posted_n = 0
    failed_n = 0
    for (r, mid, uid_key) in candidates:
        author = r["author"]
        user_info = users_state.data[author]
        token = user_info["token"]
        res = api.post_review(token, mid, r["rating"], r["content"])
        if res is None:
            # Try refresh
            login = api.login(user_info["email"], user_info["username"], SEED_PASSWORD)
            if login:
                user_info["token"] = login["accessToken"]
                users_state.save()
                res = api.post_review(login["accessToken"], mid, r["rating"], r["content"])
        if res is None:
            failed_n += 1
            posted.add(f"{uid_key}|{mid}")  # mark to skip future retries
            continue
        posted.add(f"{uid_key}|{mid}")
        per_media_count[mid] += 1
        posted_n += 1
        if posted_n % 50 == 0:
            posted_state.set("keys", list(posted))
            posted_state.save()
            print(f"posted {posted_n}; failed {failed_n}")

    posted_state.set("keys", list(posted))
    posted_state.save()
    print(f"done. posted {posted_n}; failed {failed_n}")


if __name__ == "__main__":
    main()
