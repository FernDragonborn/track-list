"""TMDB API wrapper for discover + reviews."""
import time
import requests
from typing import Iterable

from config import TMDB_API_KEY, TMDB_BASE, TMDB_SLEEP


class TmdbClient:
    def __init__(self):
        self.session = requests.Session()
        self.params = {"api_key": TMDB_API_KEY, "language": "en-US"}

    def _get(self, path: str, **extra) -> dict:
        params = {**self.params, **extra}
        r = self.session.get(f"{TMDB_BASE}{path}", params=params, timeout=20)
        time.sleep(TMDB_SLEEP)
        if r.status_code == 429:
            time.sleep(10)
            r = self.session.get(f"{TMDB_BASE}{path}", params=params, timeout=20)
        r.raise_for_status()
        return r.json()

    def discover(self, media_type: str, genre_id: int | None = None, year: int | None = None, page: int = 1) -> list[dict]:
        """media_type: 'movie' or 'tv'. Returns list of results (tmdb id, title, etc.)."""
        params: dict = {"sort_by": "popularity.desc", "page": page, "vote_count.gte": 50}
        if genre_id:
            params["with_genres"] = genre_id
        if year:
            if media_type == "movie":
                params["primary_release_year"] = year
            else:
                params["first_air_date_year"] = year
        try:
            data = self._get(f"/discover/{media_type}", **params)
            return data.get("results", []) or []
        except Exception:
            return []

    def reviews(self, media_type: str, tmdb_id: int) -> list[dict]:
        """TMDB user reviews. Returns list with author_details + content."""
        out: list[dict] = []
        try:
            page = 1
            while page <= 5:
                data = self._get(f"/{media_type}/{tmdb_id}/reviews", page=page)
                results = data.get("results", []) or []
                if not results:
                    break
                out.extend(results)
                if page >= data.get("total_pages", 1):
                    break
                page += 1
        except Exception:
            pass
        return out

    def search_movie(self, query: str) -> list[dict]:
        try:
            data = self._get("/search/movie", query=query, include_adult="false")
            return data.get("results", []) or []
        except Exception:
            return []

    def search_tv(self, query: str) -> list[dict]:
        try:
            data = self._get("/search/tv", query=query, include_adult="false")
            return data.get("results", []) or []
        except Exception:
            return []

    def search_multi(self, query: str) -> list[dict]:
        try:
            data = self._get("/search/multi", query=query, include_adult="false")
            return [r for r in (data.get("results") or []) if r.get("media_type") in ("movie", "tv")]
        except Exception:
            return []
