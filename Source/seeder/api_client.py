"""Client wrapper for TrackList API."""
import time
import requests
from typing import Optional

from config import (
    API_BASE, ADMIN_USERNAME, ADMIN_PASSWORD, ADMIN_EMAIL,
    ADMIN_SETUP_TOKEN, SEED_PASSWORD, API_SLEEP,
)


class ApiClient:
    def __init__(self):
        self.session = requests.Session()
        self.session.headers["User-Agent"] = "tracklist-seeder/1.0"

    def _sleep(self):
        if API_SLEEP > 0:
            time.sleep(API_SLEEP)

    # --- auth ---
    def login(self, email: str, username: str, password: str) -> Optional[dict]:
        try:
            r = self.session.post(
                f"{API_BASE}/auth/login",
                json={"Email": email, "Username": username, "Password": password},
                timeout=10,
            )
            self._sleep()
            if r.status_code == 200:
                return r.json()["data"]
            return None
        except Exception:
            return None

    def login_admin(self) -> Optional[str]:
        d = self.login(ADMIN_EMAIL, ADMIN_USERNAME, ADMIN_PASSWORD)
        return d["accessToken"] if d else None

    def setup_admin(self) -> tuple[bool, str]:
        headers = {}
        if ADMIN_SETUP_TOKEN:
            headers["X-Setup-Token"] = ADMIN_SETUP_TOKEN

        try:
            r = self.session.post(
                f"{API_BASE}/setup/admin",
                headers=headers,
                json={
                    "Email": ADMIN_EMAIL,
                    "Username": ADMIN_USERNAME,
                    "Password": ADMIN_PASSWORD,
                    "ConfirmPassword": ADMIN_PASSWORD,
                    "SetupToken": ADMIN_SETUP_TOKEN or None,
                },
                timeout=15,
            )
            self._sleep()
            if r.status_code == 200:
                return True, "admin setup completed"
            if r.status_code == 409:
                return False, "setup is already complete; verify SEED_ADMIN_EMAIL, SEED_ADMIN_USERNAME and SEED_ADMIN_PASSWORD"
            return False, f"admin setup failed ({r.status_code}): {self._error_message(r)}"
        except Exception as ex:
            return False, f"admin setup request failed: {ex}"

    def ensure_admin(self) -> tuple[Optional[str], Optional[str]]:
        token = self.login_admin()
        if token:
            return token, None

        ok, message = self.setup_admin()
        if not ok:
            return None, message

        token = self.login_admin()
        if token:
            return token, None

        return None, "admin setup completed, but login still failed; verify seeder admin credentials"

    def register(self, email: str, username: str, password: str = SEED_PASSWORD) -> Optional[dict]:
        try:
            r = self.session.post(
                f"{API_BASE}/profiles/register",
                json={
                    "Email": email,
                    "Username": username,
                    "Password": password,
                    "ConfirmPassword": password,
                },
                timeout=15,  # BCrypt
            )
            self._sleep()
            if r.status_code == 200:
                return r.json()["data"]
            return None
        except Exception:
            return None

    @staticmethod
    def _error_message(response: requests.Response) -> str:
        try:
            body = response.json()
            if isinstance(body, dict):
                return body.get("error") or body.get("message") or str(body)
            return str(body)
        except Exception:
            return response.text.strip() or response.reason

    # --- media ---
    def get_media_by_external_id(self, external_id: str) -> Optional[dict]:
        """Triggers backend auto-import from TMDB if missing."""
        try:
            r = self.session.get(f"{API_BASE}/media/{external_id}", timeout=20)
            self._sleep()
            if r.status_code == 200:
                return r.json()["data"]
            return None
        except Exception:
            return None

    def search_media(self, query: str) -> list[dict]:
        try:
            r = self.session.get(f"{API_BASE}/media/search", params={"query": query}, timeout=20)
            self._sleep()
            if r.status_code == 200:
                return r.json().get("data", []) or []
            return []
        except Exception:
            return []

    # --- profile updates ---
    def set_avatar_url(self, token: str, current_profile: dict, profile_pic_url: str) -> bool:
        """PUT /profiles/me with current data + new pic URL."""
        # endpoint expects full user DTO; build a minimal payload from existing
        body = {
            "username": current_profile["username"],
            "email": current_profile["email"],
            "role": current_profile.get("role"),
            "gender": current_profile.get("gender") or "male",
            "country": current_profile.get("country") or "",
            "profilePicUrl": profile_pic_url,
            "displayName": current_profile.get("displayName"),
            "bio": current_profile.get("bio"),
        }
        try:
            r = self.session.put(
                f"{API_BASE}/profiles/me",
                headers={"Authorization": f"Bearer {token}"},
                json=body,
                timeout=10,
            )
            self._sleep()
            return r.status_code in (200, 204)
        except Exception:
            return False

    def get_user_profile(self, username: str) -> Optional[dict]:
        try:
            r = self.session.get(f"{API_BASE}/profiles/{username}", timeout=10)
            self._sleep()
            if r.status_code == 200:
                return r.json()["data"]
            return None
        except Exception:
            return None

    # --- reviews ---
    def post_review(self, token: str, media_guid: str, rating: int, content: str | None) -> Optional[dict]:
        try:
            r = self.session.post(
                f"{API_BASE}/media/{media_guid}/reviews",
                headers={"Authorization": f"Bearer {token}"},
                json={"Rating": rating, "Content": content or ""},
                timeout=15,
            )
            self._sleep()
            if r.status_code == 200:
                return r.json()["data"]
            return None
        except Exception:
            return None
