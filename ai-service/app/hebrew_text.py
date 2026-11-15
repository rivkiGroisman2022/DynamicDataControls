"""Hebrew text helpers: normalization and phrase matching that tolerates prefixes (ו, ש, ה, ב, ל, מ, כ)."""

from __future__ import annotations

import re
from dataclasses import dataclass

HEB = "א-ת"
_PREFIX = "[ושהבלמכ]{0,3}"
_NIQQUD = re.compile("[֑-ׇ]")


def normalize(text: str) -> str:
    """Unifies the text: no niqqud, maqaf/dash → space, unified quotes, numbers without thousands separators."""
    # The maqaf (U+05BE) is inside the niqqud range, so it must be replaced with a space BEFORE niqqud is removed.
    t = re.sub(r"[־–—]", " ", text)
    t = t.replace("״", '"').replace("׳", "'").replace("“", '"').replace("”", '"')
    t = _NIQQUD.sub("", t)
    t = re.sub(r"(?<=\d),(?=\d{3}\b)", "", t)                      # 15,000 → 15000
    t = re.sub(r"(\d+(?:\.\d+)?)\s*אלף", lambda m: str(int(float(m.group(1)) * 1000)), t)  # 15 אלף → 15000
    t = re.sub(r"(?<=[" + HEB + r"])-(?=\d)", " ", t)              # מ-18 → מ 18
    return re.sub(r"\s+", " ", t).strip()


def _inflected(word: str) -> str:
    """
    Tolerance for common Hebrew inflections at the end of a word:
    "העסקה" → also "העסקת" / "העסקתם" / "העסקתו"; "פעיל" → also "פעילים" / "פעילה"; "שכר" → "שכרם".
    """
    if not re.fullmatch(f"[{HEB}]+", word) or len(word) < 3:
        return re.escape(word)
    if word.endswith("ה"):
        stem = re.escape(word[:-1])
        return f"{stem}(?:ה|ת(?:ם|ן|ו|ה|נו)?)"
    return re.escape(word) + "(?:ים|ות|ם|ן|ו|ה)?"


def phrase_regex(phrase: str) -> re.Pattern[str]:
    """
    A pattern for a Hebrew phrase: each word may come with prefix letters ("ומשכורתם", "מתאריך", "ההעסקה")
    and an inflection suffix ("העסקתם", "פעילים"), and the match is a whole word only (no letter before or after).
    """
    words = normalize(phrase).split()
    body = r"\s+".join(_PREFIX + _inflected(w) for w in words)
    return re.compile(rf"(?<![{HEB}A-Za-z]){body}(?![{HEB}A-Za-z])")


@dataclass(frozen=True)
class Span:
    start: int
    end: int
    text: str


def find_all(phrase: str, text: str) -> list[Span]:
    if not phrase or not phrase.strip():
        return []
    return [Span(m.start(), m.end(), m.group(0)) for m in phrase_regex(phrase).finditer(text)]


def has_word(pattern: str, text: str) -> re.Match[str] | None:
    """Searches for a pattern as a whole word (allowing prefixes)."""
    return re.search(rf"(?<![{HEB}]){_PREFIX}(?:{pattern})(?![{HEB}])", text)
