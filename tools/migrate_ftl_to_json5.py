#!/usr/bin/env python3
"""One-shot: parse OpenRA-style .ftl into dict, merge into *.json5, fix map.yaml, then delete .ftl (run delete separately)."""
from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MODS = ROOT / "mods"


def normalize_placeholders(s: str) -> str:
    s = re.sub(r"\{\s*\$([a-zA-Z0-9_-]+)\s*\}", r"{\1}", s)
    return s


def strip_json5_comments_for_parse(text: str) -> str:
    out_lines = []
    for line in text.splitlines():
        t = line.lstrip()
        if t.startswith("//"):
            continue
        out_lines.append(line)
    text = "\n".join(out_lines)
    text = re.sub(r",(\s*[\]}])", r"\1", text)
    return text


def load_json_dict(path: Path) -> dict[str, str]:
    if not path.is_file() or path.stat().st_size < 3:
        return {}
    raw = path.read_text(encoding="utf-8")
    raw = strip_json5_comments_for_parse(raw)
    try:
        d = json.loads(raw)
    except json.JSONDecodeError as e:
        print(f"WARN: could not parse {path}: {e}", file=sys.stderr)
        return {}
    return {k: v for k, v in d.items() if isinstance(v, str)}


def save_json_dict(path: Path, data: dict[str, str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    keys = sorted(data.keys(), key=str.lower)
    lines = ["{"]
    for i, k in enumerate(keys):
        v = data[k]
        com = "," if i < len(keys) - 1 else ""
        lines.append("\t" + json.dumps(k, ensure_ascii=False) + ": " + json.dumps(v, ensure_ascii=False) + com)
    lines.append("}")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def parse_ftl(text: str) -> dict[str, str]:
    lines = text.splitlines()
    out: dict[str, str] = {}
    i = 0
    n = len(lines)

    def is_top_key(line: str) -> bool:
        return bool(re.match(r"^[a-zA-Z][a-zA-Z0-9_.-]*\s*=", line))

    while i < n:
        line = lines[i]
        raw = line.rstrip()
        if not raw.strip() or raw.strip().startswith("#"):
            i += 1
            continue

        if not is_top_key(raw):
            i += 1
            continue

        m = re.match(r"^([a-zA-Z][a-zA-Z0-9_.-]*)\s*=\s*(.*)$", raw)
        if not m:
            i += 1
            continue
        key, rest = m.group(1), m.group(2).strip()
        i += 1

        if rest and not (rest == "{" or "->" in rest):
            out[key] = normalize_placeholders(rest)
            continue

        block_lines: list[str] = []
        while i < n:
            L = lines[i]
            if is_top_key(L.strip()):
                break
            block_lines.append(L)
            i += 1

        block = "\n".join(block_lines)

        for am in re.finditer(
            r"(?m)^\s+\.([a-zA-Z][a-zA-Z0-9_-]*)\s*=\s*(.*)$", block
        ):
            attr = am.group(1)
            val_start = am.group(2).rstrip()
            parts = [val_start] if val_start else []
            line_no = block[: am.start()].count("\n")
            sub = block_lines[line_no + 1 :] if line_no + 1 <= len(block_lines) else []
            for row in sub:
                if re.match(r"^\s+\.", row) or (row.strip() and is_top_key(row.strip())):
                    break
                if not row.strip():
                    continue
                parts.append(row.strip())
            val = " ".join(parts).strip()
            out[f"{key}.{attr}"] = normalize_placeholders(val)

        om = re.search(r"(?m)^\s*\*\[other\]\s*(.+)$", block)
        if om:
            out[key] = normalize_placeholders(om.group(1).strip())
        elif f"{key}.label" in out or f"{key}.name" in out:
            pass
        elif not any(k == key or k.startswith(key + ".") for k in out):
            one = re.search(r"(?m)^\s*\[one\]\s*(.+)$", block)
            if one:
                out[key] = normalize_placeholders(one.group(1).strip())

    return out


def merge_ftl_files(paths: list[Path], target: Path, json_wins: bool = True) -> None:
    merged = load_json_dict(target)
    for p in paths:
        if not p.is_file():
            print(f"missing ftl {p}", file=sys.stderr)
            continue
        fd = parse_ftl(p.read_text(encoding="utf-8"))
        for k, v in fd.items():
            if json_wins and k in merged:
                continue
            merged[k] = v
    save_json_dict(target, merged)
    print(f"updated {target} ({len(merged)} keys)")


def map_yaml_process() -> None:
    for map_yaml in MODS.rglob("map.yaml"):
        text = map_yaml.read_text(encoding="utf-8")
        if "FluentMessages:" not in text:
            continue
        m = re.search(r"(?m)^FluentMessages:\s*(.+)$", text)
        if not m:
            continue
        val = m.group(1).strip()
        map_dir = map_yaml.parent
        has_local_ftl = (map_dir / "map.ftl").is_file()

        insert_trans = has_local_ftl
        new_text = re.sub(r"(?m)^FluentMessages:.*\n?", "", text)

        if insert_trans:
            if "Translations:" not in new_text:
                # Map string[] field uses the node's scalar Value (see MapField.Deserialize).
                chunk = "\nTranslations: map-en.json5\n"
                new_text = new_text.rstrip() + chunk + "\n"
        if new_text != text:
            map_yaml.write_text(new_text, encoding="utf-8")
            print(f"map.yaml: {map_yaml.relative_to(ROOT)}")


def main() -> int:
    merge_ftl_files(
        [
            MODS / "common/fluent/common.ftl",
            MODS / "common/fluent/rules.ftl",
            MODS / "common/fluent/hotkeys.ftl",
            MODS / "common/fluent/chrome.ftl",
            MODS / "common-content/fluent/content.ftl",
            MODS / "common-content/fluent/chrome.ftl",
        ],
        MODS / "common/languages/en.json5",
    )

    merge_ftl_files(
        [
            MODS / "ra/fluent/ra.ftl",
            MODS / "ra/fluent/chrome.ftl",
            MODS / "ra/fluent/hotkeys.ftl",
            MODS / "ra/fluent/rules.ftl",
            MODS / "ra/fluent/lua.ftl",
            MODS / "ra/fluent/campaign.ftl",
            MODS / "ra-content/fluent/chrome.ftl",
        ],
        MODS / "ra/languages/en.json5",
    )

    merge_ftl_files(
        [
            MODS / "cnc/fluent/cnc.ftl",
            MODS / "cnc/fluent/chrome.ftl",
            MODS / "cnc/fluent/hotkeys.ftl",
            MODS / "cnc/fluent/rules.ftl",
            MODS / "cnc/fluent/lua.ftl",
            MODS / "cnc/fluent/campaign.ftl",
            MODS / "cnc-content/fluent/chrome.ftl",
        ],
        MODS / "cnc/languages/en.json5",
    )

    merge_ftl_files(
        [
            MODS / "d2k/fluent/d2k.ftl",
            MODS / "d2k/fluent/chrome.ftl",
            MODS / "d2k/fluent/hotkeys.ftl",
            MODS / "d2k/fluent/rules.ftl",
            MODS / "d2k/fluent/lua.ftl",
            MODS / "d2k/fluent/campaign.ftl",
            MODS / "d2k-content/fluent/chrome.ftl",
        ],
        MODS / "d2k/languages/en.json5",
    )

    merge_ftl_files(
        [
            MODS / "ts/fluent/ts.ftl",
            MODS / "ts/fluent/chrome.ftl",
            MODS / "ts/fluent/hotkeys.ftl",
            MODS / "ts/fluent/rules.ftl",
            MODS / "ts-content/fluent/chrome.ftl",
        ],
        MODS / "ts/languages/en.json5",
    )

    for map_ftl in MODS.rglob("map.ftl"):
        rel = map_ftl.relative_to(MODS)
        out = map_ftl.parent / "map-en.json5"
        d = parse_ftl(map_ftl.read_text(encoding="utf-8"))
        save_json_dict(out, d)
        print(f"map-en.json5 <- {rel} ({len(d)} keys)")

    map_yaml_process()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
