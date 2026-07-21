"""Set DiceBear PNG avatars for all seeded users."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from state import State  # noqa: E402
from api_client import ApiClient  # noqa: E402
from config import SEED_PASSWORD  # noqa: E402


def main():
    api = ApiClient()
    users_state = State("users")
    n_ok = 0
    n_fail = 0
    for handle, info in users_state.data.items():
        if info.get("avatar_png_set"):
            continue
        username = info["username"]
        png_url = f"https://api.dicebear.com/7.x/avataaars/png?seed={username}&size=200"

        profile = api.get_user_profile(username)
        if not profile:
            n_fail += 1
            continue
        token = info["token"]
        ok = api.set_avatar_url(token, profile, png_url)
        if not ok:
            # try refresh login
            login = api.login(info["email"], username, SEED_PASSWORD)
            if login:
                info["token"] = login["accessToken"]
                users_state.save()
                ok = api.set_avatar_url(login["accessToken"], profile, png_url)
        if ok:
            info["avatar_url"] = png_url
            info["avatar_png_set"] = True
            n_ok += 1
            if n_ok % 20 == 0:
                users_state.save()
                print(f"avatars set: {n_ok}")
        else:
            n_fail += 1
            if n_fail <= 3:
                print(f"  fail for {username}")
    users_state.save()
    print(f"done. ok={n_ok}, fail={n_fail}")


if __name__ == "__main__":
    main()
