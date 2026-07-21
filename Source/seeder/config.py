"""Seeder configuration. All knobs in one place."""
import os
from pathlib import Path

# Optional: load .env from this directory if python-dotenv is installed
try:
    from dotenv import load_dotenv
    load_dotenv(Path(__file__).parent / ".env")
except ImportError:
    pass

# API
API_BASE = os.environ.get("SEED_API_BASE", "http://localhost/api")
ADMIN_USERNAME = os.environ.get("SEED_ADMIN_USERNAME", "admin")
ADMIN_PASSWORD = os.environ.get("SEED_ADMIN_PASSWORD", "seedPassword1")
ADMIN_EMAIL = os.environ.get("SEED_ADMIN_EMAIL", "admin@tracklist.local")
ADMIN_SETUP_TOKEN = os.environ.get("SEED_SETUP_TOKEN") or os.environ.get("TRACKLIST_SETUP_TOKEN", "")

# TMDB — match backend variable name (TMDBApiKey) so the same .env line
# works for both. Falls back to TMDB_API_KEY for older configs.
TMDB_API_KEY = os.environ.get("TMDBApiKey") or os.environ.get("TMDB_API_KEY", "")
TMDB_BASE = "https://api.themoviedb.org/3"

# Reddit (anon JSON)
REDDIT_BASE = "https://www.reddit.com"
REDDIT_UA = "tracklist-seeder/1.0 (+by /u/tracklist-academic)"

# Targets
SEED_PASSWORD = "password1"  # validator requires 8+ chars, letter+digit. User wanted "password" but constraint forces digit.
MAX_USERNAME_LEN = 25
MAX_EMAIL_LEN = 50
MAX_CONTENT_LEN = 9900  # truncate threshold (DB MaxLength 10000)
LONG_REVIEW_MIN_CHARS = 500
SHORT_REVIEW_MAX_CHARS = 200

TARGET_LONG_REVIEWS = 500
TARGET_SHORT_REVIEWS = 2000
TARGET_MEDIA_COUNT = 300
TARGET_USERS = 100  # cap — may have fewer if scraping yields fewer unique handles
PER_MEDIA_REVIEW_CAP = 50  # spread distribution

# Rate limits
REDDIT_SLEEP = 1.2
TMDB_SLEEP = 0.3
API_SLEEP = 0.0  # local

# Paths
ROOT = Path(__file__).parent
DATA_DIR = ROOT / "data"
SCRAPED_DIR = ROOT / "scraped"
LOG_FILE = ROOT / "seeder.log"

DATA_DIR.mkdir(exist_ok=True)
SCRAPED_DIR.mkdir(exist_ok=True)

# Subreddits to scrape (variety of media types/tone)
SUBREDDITS = [
    "movies",
    "television",
    "MovieReviews",
    "TelevisionReviews",
    "TrueFilm",
    "horror",
    "marvelstudios",
    "StarWars",
    "anime",
    "criterion",
]

# TMDB genre ids (movie) — for diversified discover queries
MOVIE_GENRES = [
    28,  # Action
    12,  # Adventure
    16,  # Animation
    35,  # Comedy
    80,  # Crime
    99,  # Documentary
    18,  # Drama
    10751,  # Family
    14,  # Fantasy
    36,  # History
    27,  # Horror
    10402,  # Music
    9648,  # Mystery
    10749,  # Romance
    878,  # SciFi
    53,  # Thriller
    10752,  # War
    37,  # Western
]
TV_GENRES = [
    10759, 16, 35, 80, 99, 18, 10751, 10762, 9648, 10763, 10764, 10765, 10766, 10767, 10768, 37,
]

DISCOVER_YEARS = [2024, 2023, 2022, 2020, 2018, 2015, 2010, 2005, 2000, 1995, 1990]
