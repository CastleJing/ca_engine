#!/usr/bin/env python3
"""Second-pass legacy UI translation migration.

- dialog-* → Chrome-Dialog-{PascalSlug}[-{PascalTail}]
- hotkey-description-* → HotkeyDescription-{HotkeyName} (matched against Hotkey-* keys)
- image-* tooltips, dropdownbutton-* → explicit Chrome-* targets
- Adds Chrome-ModContent-* for common-content installer strings

Run: python3 CAmod/engine/tools/migrate_chrome_legacy_batch2.py
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

SCRIPT = Path(__file__).resolve()
ENGINE = SCRIPT.parent.parent
ROOT = ENGINE.parent
SKIP_DIRS = frozenset({"bin", "obj", ".git"})
EXTS = frozenset({".json5", ".yaml", ".yml", ".cs"})
_JSON5_KEY = re.compile(r'^[\t ]*"([^"]+)":')


def kebab_parts_to_pascal(slug: str) -> str:
    return "".join(
        (p[0].upper() + p[1:].lower()) if p else ""
        for p in slug.split("-")
        if p
    )


def map_dialog_key(old: str) -> str | None:
    if not old.startswith("dialog-"):
        return None
    rest = old[len("dialog-") :]
    if "." in rest:
        slug, tail = rest.split(".", 1)
        return f"Chrome-Dialog-{kebab_parts_to_pascal(slug)}-{kebab_parts_to_pascal(tail)}"
    return f"Chrome-Dialog-{kebab_parts_to_pascal(rest)}"


STATIC_REPLACEMENTS: list[tuple[str, str]] = [
    ("image-lobby-servers-bin-password-protected-tooltip", "Chrome-LobbyServers-NeedPasswd"),
    ("image-lobby-servers-bin-requires-authentication-tooltip", "Chrome-LobbyServers-NeedAccount"),
    ("image-multiplayer-panel-password-protected-tooltip", "Chrome-MPBrowser-NeedPasswd"),
    ("image-multiplayer-panel-requires-authentication-tooltip", "Chrome-MPBrowser-NeedAccount"),
    ("image-bg-password-protected-tooltip", "Chrome-MPBrowser-NeedPasswd"),
    ("image-bg-requires-authentication-tooltip", "Chrome-MPBrowser-NeedAccount"),
    ("dropdownbutton-assetbrowser-asset-types-dropdown", "Chrome-AssetBrowser-AssetTypesDropdown"),
    ("dropdownbutton-assetbrowser-source-selector", "Chrome-AssetBrowser-SourceSelector"),
    ("dropdownbutton-display-selection-container-dropdown", "Chrome-Setting-Display-Display-Dropdown"),
    ("dropdownbutton-filters-flt-player", "Chrome-ReplayBrowser-Filter-Player-Dropdown"),
    ("dropdownbutton-hpf-overlay-check", "Chrome-InGameDebug-HpfOverlay-BlockedByActor"),
    ("dropdownbutton-hpf-overlay-locomotor", "Chrome-InGameDebug-HpfOverlay-Locomotor"),
    ("dropdownbutton-lobby-players-handicap-tooltip", "Chrome-LobbyPlayer-HandicpDropDown-Tooltip"),
    ("dropdownbutton-lobby-servers-bin-filters", "Chrome-LobbyServers-Filter"),
    ("dropdownbutton-multiplayer-panel-filters", "Chrome-LobbyServers-Filter"),
    ("dropdownbutton-news-bg-button", "Chrome-MainMenu-NewButton"),
    ("dropdownbutton-save-map-panel-visibility-dropdown", "Chrome-Editor-SaveMap-Visibility-Dropdown"),
    ("dropdownbutton-save-map-background-visibility-dropdown", "Chrome-Editor-SaveMap-Visibility-Dropdown"),
    ("dropdownbutton-server-lobby-slots", "Chrome-Lobby-SlotsAdmin"),
    ("dropdownbutton-video-mode-dropdown-container", "Chrome-Setting-Display-VideoMode-Dropdown"),
    ("button-package-template-download", "Chrome-ModContent-DownloadPackage"),
    ("button-content-panel-check-source", "Chrome-ModContent-DetectSource"),
    ("button-content-prompt-panel-advanced", "Chrome-ModContent-AdvancedInstall"),
    ("button-content-prompt-panel-quick", "Chrome-ModContent-QuickInstall"),
    ("button-quit", "Chrome-MainMenu-Quit"),
    ("button-directconnect-panel-join", "Chrome-MPDriectConnect-OkButton"),
    ("button-kick-client-dialog", "Chrome-LobbyKickDialogs-OkButton"),
    ("button-kick-spectators-dialog-ok", "Chrome-LobbyKickSpectatorsDialogs-OkButton"),
    ("button-force-start-dialog-start", "Chrome-LobbyForceStartDialogs-OkButton"),
    ("button-continue", "Chrome-MainMenu-Prompts-Contine"),
    ("button-debug-panel-give-cash", "Chrome-InGameDebug-GameCash"),
    ("button-debug-panel-grow-resources", "Chrome-InGameDebug-GrowResources"),
    ("button-debug-panel-give-exploration", "Chrome-InGameDebug-GiveExploration"),
    ("button-debug-panel-reset-exploration", "Chrome-InGameDebug-ResetExploration"),
    ("button-color-chooser-random", "Chrome-ColorPicker-RandomButton"),
    ("button-color-chooser-store", "Chrome-ColorPicker-StoreButton"),
    ("button-color-chooser-mixer-tab", "Chrome-ColorPicker-MixerTabButton"),
    ("button-color-chooser-palette-tab", "Chrome-ColorPicker-PaletteTabButton"),
    ("button-prompt-confirm", "Chrome-ConfirmationDialogs-ConfirmButton"),
    ("button-prompt-other", "Chrome-ConfirmationDialogs-OtherButton"),
    ("button-text-input-prompt-accept", "Chrome-ConfirmationDialogs-AcceptButton"),
    ("button-new-map-bg-create", "Chrome-Editor-NewMap-Create"),
    ("button-save-map-panel", "Chrome-Editor-SaveMap-Save"),
    ("button-marker-tiles-clear-current", "Chrome-Editor-MarkerClearCurrent"),
    ("button-marker-tiles-clear-all", "Chrome-Editor-MarkerClearAll"),
    ("button-container-ok", "Chrome-Editor-OkButton"),
    ("button-select-categories-buttons-all", "Chrome-Editor-Filter-All"),
    ("button-select-categories-buttons-none", "Chrome-Editor-Filter-None"),
]


def collect_json5_keys(root: Path) -> set[str]:
    keys: set[str] = set()
    for path in root.rglob("*.json5"):
        if any(p in SKIP_DIRS for p in path.parts):
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except OSError:
            continue
        for line in text.splitlines():
            m = _JSON5_KEY.match(line)
            if m:
                keys.add(m.group(1))
    return keys


def build_hotkey_description_map(all_keys: set[str]) -> dict[str, str]:
    hk_names: dict[str, str] = {}
    for k in all_keys:
        if not k.startswith("Hotkey-") or k.startswith("HotkeyDescription-"):
            continue
        name = k[len("Hotkey-") :]
        hk_names[name.lower()] = name
    out: dict[str, str] = {}
    for k in all_keys:
        if not k.startswith("hotkey-description-"):
            continue
        x = k[len("hotkey-description-") :]
        if x in hk_names:
            out[k] = f"HotkeyDescription-{hk_names[x]}"
        else:
            out[k] = f"HotkeyDescription-{x[0].upper() + x[1:]}" if x else k
    return out


def build_dialog_map(all_keys: set[str]) -> dict[str, str]:
    return {k: v for k in all_keys if (v := map_dialog_key(k))}


def merge_replacements(root: Path) -> list[tuple[str, str]]:
    keys = collect_json5_keys(root)
    rep: dict[str, str] = {}
    rep.update(build_dialog_map(keys))
    rep.update(build_hotkey_description_map(keys))
    for old, new in STATIC_REPLACEMENTS:
        rep[old] = new
    items = list(rep.items())
    items.sort(key=lambda x: len(x[0]), reverse=True)
    return items


def apply_text(text: str, rep: list[tuple[str, str]]) -> str:
    for old, new in rep:
        if old in text:
            text = text.replace(old, new)
    return text


def dedupe_json5(text: str) -> str:
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
    text = apply_text(text, rep)
    if path.suffix.lower() == ".json5":
        text = dedupe_json5(text)
    if text == orig:
        return False
    path.write_text(text, encoding="utf-8")
    return True


def ensure_modcontent_keys(en_path: Path) -> None:
    """Insert Chrome-ModContent-* if missing (English defaults)."""
    if not en_path.is_file():
        return
    text = en_path.read_text(encoding="utf-8")
    need = []
    if "Chrome-ModContent-DownloadPackage" not in text:
        need.append('\t"Chrome-ModContent-DownloadPackage": "Download",')
    if "Chrome-ModContent-DetectSource" not in text:
        need.append('\t"Chrome-ModContent-DetectSource": "Detect Disc or Installation",')
    if "Chrome-ModContent-AdvancedInstall" not in text:
        need.append('\t"Chrome-ModContent-AdvancedInstall": "Advanced Install",')
    if "Chrome-ModContent-QuickInstall" not in text:
        need.append('\t"Chrome-ModContent-QuickInstall": "Quick Install",')
    if not need:
        return
    insert = "\n".join(need) + "\n"
    marker = '\t"Chrome-AssetBrowser-AssetTypesDropdown":'
    if marker in text:
        text = text.replace(marker, insert + marker, 1)
        en_path.write_text(text, encoding="utf-8")


def patch_editor_cancel(yaml_path: Path) -> bool:
    if not yaml_path.is_file():
        return False
    text = yaml_path.read_text(encoding="utf-8")
    if "Text: button-cancel" not in text:
        return False
    text2 = text.replace("Text: button-cancel", "Text: Chrome-Editor-CancelButton")
    if text2 == text:
        return False
    yaml_path.write_text(text2, encoding="utf-8")
    return True


def patch_confirmation_cancel(yaml_path: Path) -> bool:
    if not yaml_path.is_file():
        return False
    text = yaml_path.read_text(encoding="utf-8")
    if "Text: button-cancel" not in text:
        return False
    text2 = text.replace("Text: button-cancel", "Text: Chrome-ConfirmationDialogs-CancelButton")
    if text2 == text:
        return False
    yaml_path.write_text(text2, encoding="utf-8")
    return True


def patch_content_cancel(yaml_path: Path) -> bool:
    if not yaml_path.is_file():
        return False
    text = yaml_path.read_text(encoding="utf-8")
    if "Text: button-cancel" not in text:
        return False
    text2 = text.replace("Text: button-cancel", "Text: Chrome-ConfirmationDialogs-CancelButton")
    yaml_path.write_text(text2, encoding="utf-8")
    return text2 != text


def patch_multiplayer_dc_cancel(yaml_path: Path) -> bool:
    if not yaml_path.is_file():
        return False
    text = yaml_path.read_text(encoding="utf-8")
    if "Text: button-cancel" not in text:
        return False
    text2 = text.replace("Text: button-cancel", "Text: Chrome-MPDriectConnect-BackButton")
    yaml_path.write_text(text2, encoding="utf-8")
    return text2 != text


def patch_lobby_kickdialogs_cancel(yaml_path: Path) -> bool:
    if not yaml_path.is_file():
        return False
    text = yaml_path.read_text(encoding="utf-8")
    if "Text: button-cancel" not in text:
        return False
    text2 = text.replace("Text: button-cancel", "Text: Chrome-LobbyKickDialogs-CancelButton")
    yaml_path.write_text(text2, encoding="utf-8")
    return text2 != text


def main() -> int:
    if not ROOT.is_dir():
        print("missing", ROOT, file=sys.stderr)
        return 1
    rep = merge_replacements(ROOT)
    print("replacements:", len(rep))
    changed = 0
    for path in ROOT.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in EXTS:
            continue
        if any(p in SKIP_DIRS for p in path.parts):
            continue
        if migrate_file(path, rep):
            changed += 1
            print(path.relative_to(ROOT))

    ensure_modcontent_keys(ROOT / "engine" / "mods" / "common" / "languages" / "en.json5")

    # Per-context button-cancel
    if patch_editor_cancel(ROOT / "engine" / "mods" / "cnc" / "chrome" / "editor.yaml"):
        print("patched cnc/chrome/editor.yaml (cancel→Chrome-Editor-CancelButton)")
        changed += 1
    if patch_lobby_kickdialogs_cancel(ROOT / "engine" / "mods" / "cnc" / "chrome" / "lobby-kickdialogs.yaml"):
        print("patched cnc/chrome/lobby-kickdialogs.yaml")
        changed += 1
    if patch_multiplayer_dc_cancel(ROOT / "engine" / "mods" / "cnc" / "chrome" / "multiplayer-directconnect.yaml"):
        print("patched cnc/chrome/multiplayer-directconnect.yaml")
        changed += 1
    if patch_confirmation_cancel(ROOT / "engine" / "mods" / "cnc" / "chrome" / "dialogs.yaml"):
        print("patched cnc/chrome/dialogs.yaml")
        changed += 1
    if patch_content_cancel(ROOT / "engine" / "mods" / "common-content" / "content.yaml"):
        print("patched common-content/content.yaml (package cancel)")
        changed += 1

    print("files touched (migrate + patches):", changed)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
