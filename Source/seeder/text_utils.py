"""Text helpers: slugify usernames, extract ratings, classify length."""
import re

from config import MAX_USERNAME_LEN, MAX_EMAIL_LEN, MAX_CONTENT_LEN


_USERNAME_BAD = re.compile(r"[^a-zA-Z0-9_-]+")


def slugify_username(handle: str) -> str:
    """Slug a Reddit/TMDB author handle into a valid app username (max 25 chars, alnum+underscore+dash)."""
    if not handle:
        return ""
    s = handle.strip().lstrip("/u/").lstrip("u/")
    s = _USERNAME_BAD.sub("_", s)
    s = s.strip("_-")
    if not s:
        return ""
    if s[0].isdigit():
        s = "u_" + s
    return s[:MAX_USERNAME_LEN]


def make_email(username: str) -> str:
    """Generate email under 50-char limit."""
    email = f"{username}@example.com"
    if len(email) > MAX_EMAIL_LEN:
        # Truncate username portion to fit
        max_local = MAX_EMAIL_LEN - len("@example.com")
        email = f"{username[:max_local]}@example.com"
    return email


_RATING_PATTERNS = [
    re.compile(r"(\d{1,2})\s*/\s*10\b"),
    re.compile(r"\b([0-9](?:\.\d)?)\s*/\s*10\b"),
    re.compile(r"\b(\d{1,2})\s*out\s*of\s*10\b", re.IGNORECASE),
    re.compile(r"\brating\s*[:\-]\s*(\d{1,2})\b", re.IGNORECASE),
    re.compile(r"\b(\d{1,2})\s*stars?\b", re.IGNORECASE),
]


def extract_rating(text: str) -> int | None:
    """Pull explicit numeric rating from review text; clamp 1..10."""
    if not text:
        return None
    for pat in _RATING_PATTERNS:
        m = pat.search(text)
        if m:
            try:
                r = float(m.group(1))
                if r <= 5:  # 5-star scale → upscale (e.g. "3 stars" = 6/10)
                    if pat.pattern.startswith(r"\b(\d{1,2})\s*stars"):
                        r = r * 2
                if r < 1:
                    continue
                if r > 10:
                    r = 10
                return int(round(r))
            except Exception:
                continue
    return None


_POSITIVE = {"amazing", "loved", "fantastic", "brilliant", "masterpiece", "incredible", "perfect", "great", "excellent", "best", "wonderful", "outstanding", "stunning"}
_NEGATIVE = {"boring", "worst", "awful", "terrible", "bad", "disappointing", "trash", "garbage", "horrible", "hated", "mediocre", "dull", "stupid", "broken"}


def sentiment_rating(text: str) -> int:
    """Heuristic sentiment → 1-10. Skewed positive (Reddit tends optimistic)."""
    if not text:
        return 7
    tl = text.lower()
    pos = sum(1 for w in _POSITIVE if w in tl)
    neg = sum(1 for w in _NEGATIVE if w in tl)
    diff = pos - neg
    if diff >= 3:
        return 9
    if diff >= 1:
        return 8
    if diff == 0:
        return 7
    if diff >= -2:
        return 5
    return 3


def clean_review_text(text: str) -> str:
    """Strip markdown/zalgo, normalize whitespace, truncate."""
    if not text:
        return ""
    # Drop Reddit markdown noise
    text = re.sub(r"&amp;", "&", text)
    text = re.sub(r"&gt;", ">", text)
    text = re.sub(r"&lt;", "<", text)
    text = re.sub(r"&#x200B;", "", text)
    text = re.sub(r"\[(.+?)\]\((https?://[^)]+)\)", r"\1", text)  # markdown links → label only
    text = re.sub(r"https?://\S+", "", text)  # bare urls
    text = re.sub(r"\n{3,}", "\n\n", text)
    text = text.strip()
    if len(text) > MAX_CONTENT_LEN:
        text = text[:MAX_CONTENT_LEN - 3].rstrip() + "..."
    return text


def is_long(text: str, min_chars: int) -> bool:
    return text and len(text) >= min_chars


def is_short(text: str, max_chars: int) -> bool:
    return not text or len(text) <= max_chars


# Title-cleaning for Reddit threads to derive media name
_TITLE_PREFIXES = [
    "official discussion:", "official discussion -", "official discussion",
    "[review]", "review:", "review -", "[discussion]", "discussion:", "spoilers:", "[spoilers]",
    "official discussion thread:", "thread:",
]
_TITLE_SUFFIXES = [
    "[spoilers]", "(spoilers)", "[no spoilers]", "(no spoilers)",
    "[review]", "(review)",
]


def extract_media_title(post_title: str) -> str | None:
    if not post_title:
        return None
    t = post_title.strip()
    tl = t.lower()
    for pfx in _TITLE_PREFIXES:
        if tl.startswith(pfx):
            t = t[len(pfx):].strip()
            tl = t.lower()
            break
    for sfx in _TITLE_SUFFIXES:
        if tl.endswith(sfx):
            t = t[: -len(sfx)].strip()
            tl = t.lower()
    # Strip "(2024)" year tail
    t = re.sub(r"\s*\(\d{4}\)\s*$", "", t)
    # Strip trailing " - Season X" / " season X episode Y"
    t = re.sub(r"\s*[-:]\s*season\s*\d+.*$", "", t, flags=re.IGNORECASE)
    t = re.sub(r"\s+\|\s+.+$", "", t)
    return t.strip() or None
