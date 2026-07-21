"""Idempotency state. Persist progress to JSON so reruns resume."""
import json
from pathlib import Path

from config import DATA_DIR


class State:
    def __init__(self, name: str):
        self.path: Path = DATA_DIR / f"{name}.json"
        self.data: dict = {}
        if self.path.exists():
            try:
                self.data = json.loads(self.path.read_text(encoding="utf-8"))
            except Exception:
                self.data = {}

    def save(self):
        tmp = self.path.with_suffix(".json.tmp")
        tmp.write_text(json.dumps(self.data, ensure_ascii=False, indent=2), encoding="utf-8")
        tmp.replace(self.path)

    def get(self, key, default=None):
        return self.data.get(key, default)

    def set(self, key, value):
        self.data[key] = value

    def has(self, key) -> bool:
        return key in self.data

    def __contains__(self, key) -> bool:
        return key in self.data

    def __setitem__(self, key, value):
        self.data[key] = value

    def __getitem__(self, key):
        return self.data[key]
