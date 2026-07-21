"""Reddit anonymous JSON scraper."""
import time
import requests
from typing import Iterable

from config import REDDIT_BASE, REDDIT_UA, REDDIT_SLEEP


class RedditScraper:
    def __init__(self):
        self.session = requests.Session()
        self.session.headers["User-Agent"] = REDDIT_UA

    def _get(self, url: str) -> dict | None:
        for attempt in range(3):
            try:
                r = self.session.get(url, timeout=20)
                time.sleep(REDDIT_SLEEP)
                if r.status_code == 429:
                    sleep_for = 30 + 30 * attempt
                    print(f"[reddit] 429 — sleep {sleep_for}s")
                    time.sleep(sleep_for)
                    continue
                if r.status_code == 200:
                    return r.json()
                return None
            except Exception as e:
                print(f"[reddit] err {url}: {e}")
                time.sleep(2)
        return None

    def top_posts(self, subreddit: str, t: str = "all", limit: int = 100) -> list[dict]:
        """Top posts for subreddit. t = hour|day|week|month|year|all."""
        data = self._get(f"{REDDIT_BASE}/r/{subreddit}/top.json?t={t}&limit={limit}&raw_json=1")
        if not data:
            return []
        return [c["data"] for c in (data.get("data") or {}).get("children", []) if c.get("data")]

    def comments(self, subreddit: str, post_id: str, limit: int = 200) -> list[dict]:
        """Top-level comments of a post. Returns list of comment dicts with author/body."""
        data = self._get(f"{REDDIT_BASE}/r/{subreddit}/comments/{post_id}.json?limit={limit}&raw_json=1")
        if not data or not isinstance(data, list) or len(data) < 2:
            return []
        comments_listing = data[1].get("data", {}).get("children", [])
        out: list[dict] = []

        def walk(items):
            for c in items:
                if c.get("kind") != "t1":
                    continue
                d = c.get("data") or {}
                if d.get("body") and d.get("author") and d.get("author") != "[deleted]":
                    out.append(d)
                replies = (d.get("replies") or {})
                if isinstance(replies, dict):
                    children = replies.get("data", {}).get("children", [])
                    walk(children)

        walk(comments_listing)
        return out

    def search(self, subreddit: str, query: str, limit: int = 50) -> list[dict]:
        url = f"{REDDIT_BASE}/r/{subreddit}/search.json?q={requests.utils.quote(query)}&restrict_sr=1&limit={limit}&raw_json=1&sort=relevance"
        data = self._get(url)
        if not data:
            return []
        return [c["data"] for c in (data.get("data") or {}).get("children", []) if c.get("data")]
