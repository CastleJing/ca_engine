#!/usr/bin/env python3
"""Migrate legacy translation key prefixes to Game-/Keycode- style.

- keycode.foo, keycode-modifier.alt → Keycode-Foo, Keycode-Modifier-Alt
- checkbox-slug.label|description → Game-LobbyOption-{PascalSlug}-Label|Desc
- checkbox-slug (no suffix) → Game-LobbyOption-{PascalSlug}
- dropdown-* (same rules)
- bot-slug.field → Game-Bot-{PascalSlug}-{PascalField}
- faction-slug.field → Game-Faction-{PascalSlug}-{PascalField}

Skips widget chrome names like dropdown-decorations (not present as json5 keys).

Run from repo: python3 CAmod/engine/tools/migrate_legacy_translation_patterns.py
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

SCRIPT = Path(__file__).resolve()
ENGINE = SCRIPT.parent.parent
ROOT = ENGINE.parent  # CAmod
SKIP_DIRS = frozenset({"bin", "obj", ".git"})
EXTS = frozenset({".json5", ".yaml", ".yml", ".cs"})
_JSON5_KEY = re.compile(r'^[\t ]*"([^"]+)":')


def kebab_parts_to_pascal(slug: str) -> str:
    return "".join(
        (p[0].upper() + p[1:].lower()) if p else ""
        for p in slug.split("-")
        if p
    )


def key_segment(seg: str) -> str:
    if len(seg) == 1 and seg.isalpha():
        return seg.upper()
    if seg.isdigit():
        return seg
    return seg[0].upper() + seg[1:].lower() if seg else seg


def legacy_keycode_to_new(old_key: str) -> str | None:
    if old_key.startswith("keycode-modifier."):
        rest = old_key.removeprefix("keycode-modifier.")
        prefix = "Keycode-Modifier"
    elif old_key.startswith("keycode."):
        rest = old_key.removeprefix("keycode.")
        prefix = "Keycode"
    else:
        return None
    parts = rest.split("_")
    return prefix + "-" + "-".join(key_segment(p) for p in parts)


def field_suffix(field: str) -> str:
    if field == "description":
        return "Desc"
    if field == "label":
        return "Label"
    return kebab_parts_to_pascal(field)


def lobby_option_key(prefix: str, slug: str, field: str | None) -> str:
    pascal_slug = kebab_parts_to_pascal(slug)
    base = f"Game-LobbyOption-{pascal_slug}"
    if field is None:
        return base
    return f"{base}-{field_suffix(field)}"


def game_bot_faction_key(kind: str, slug: str, field: str) -> str:
    pslug = kebab_parts_to_pascal(slug)
    fs = field_suffix(field)
    if kind == "bot":
        return f"Game-Bot-{pslug}-{fs}"
    return f"Game-Faction-{pslug}-{fs}"


def map_legacy_key(old_key: str) -> str | None:
    nk = legacy_keycode_to_new(old_key)
    if nk:
        return nk

    m = re.fullmatch(r"(checkbox|dropdown)-([^.]+)\.(label|description)", old_key)
    if m:
        return lobby_option_key(m.group(1), m.group(2), m.group(3))

    m = re.fullmatch(r"(checkbox|dropdown)-([^.]+)", old_key)
    if m:
        return lobby_option_key(m.group(1), m.group(2), None)

    m = re.fullmatch(r"bot-([^.]+)\.(.+)", old_key)
    if m:
        return game_bot_faction_key("bot", m.group(1), m.group(2))

    m = re.fullmatch(r"faction-([^.]+)\.(.+)", old_key)
    if m:
        return game_bot_faction_key("faction", m.group(1), m.group(2))

    return None


def build_replacements_from_keycode_cs(path: Path) -> dict[str, str]:
    out: dict[str, str] = {}
    if not path.is_file():
        return out
    text = path.read_text(encoding="utf-8")
    for m in re.finditer(r'"((?:keycode(?:-modifier)?)\.[^"]+)"', text):
        old = m.group(1)
        new = legacy_keycode_to_new(old)
        if new:
            out[old] = new
    return out


def collect_replacements_from_json5(root: Path) -> dict[str, str]:
    rep: dict[str, str] = {}
    kc_cs = root / "engine" / "OpenRA.Game" / "Input" / "Keycode.cs"
    rep.update(build_replacements_from_keycode_cs(kc_cs))

    for path in root.rglob("*.json5"):
        if any(p in SKIP_DIRS for p in path.parts):
            continue
        try:
            lines = path.read_text(encoding="utf-8").splitlines()
        except OSError:
            continue
        for line in lines:
            m = _JSON5_KEY.match(line)
            if not m:
                continue
            k = m.group(1)
            if k in rep:
                continue
            new_k = map_legacy_key(k)
            if new_k:
                rep[k] = new_k
    return rep


def sorted_replacements(rep: dict[str, str]) -> list[tuple[str, str]]:
    items = list(rep.items())
    items.sort(key=lambda x: len(x[0]), reverse=True)
    return items


def apply_text_replacements(text: str, rep: list[tuple[str, str]]) -> str:
    for old, new in rep:
        if old in text:
            text = text.replace(old, new)
    return text


def dedupe_json5_key_lines(text: str) -> str:
    lines = text.splitlines(keepends=True)
    key_at: dict[str, int] = {}
    for i, line in enumerate(lines):
        m = _JSON5_KEY.match(line)
        if m:
            key_at[m.group(1)] = i
    out: list[str] = []
    for i, line in enumerate(lines):
        m = _JSON5_KEY.match(line)
        if m and key_at.get(m.group(1)) != i:
            continue
        out.append(line)
    return "".join(out)


def migrate_file(path: Path, rep: list[tuple[str, str]]) -> bool:
    try:
        text = path.read_text(encoding="utf-8")
    except OSError:
        return False
    orig = text
    text = apply_text_replacements(text, rep)
    if path.suffix.lower() == ".json5":
        text2 = dedupe_json5_key_lines(text)
        text = text2
    if text == orig:
        return False
    path.write_text(text, encoding="utf-8")
    return True


def main() -> int:
    if not ROOT.is_dir():
        print("missing", ROOT, file=sys.stderr)
        return 1
    rep_dict = collect_replacements_from_json5(ROOT)
    rep = sorted_replacements(rep_dict)
    print("unique replacements:", len(rep))
    changed = 0
    for path in ROOT.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in EXTS:
            continue
        if any(p in SKIP_DIRS for p in path.parts):
            continue
        if migrate_file(path, rep):
            changed += 1
            print(path.relative_to(ROOT))
    print("files changed:", changed)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
