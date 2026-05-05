#!/usr/bin/env python3
"""Rename legacy translation keys to Game-/Chrome- style using REPLACEMENTS from migrate_translation_keys.py.

Updates *.yaml, *.yml, *.cs, *.lua under CAmod with substring replace (longest keys first).

For *.json5 only: removes lines whose key appears in EXTRA_REPLACEMENTS (old side), so duplicate
legacy entries next to canonical Chrome-* keys are dropped; mod files fall back to common.
"""
from __future__ import annotations

import importlib.util
import re
import sys
from pathlib import Path

ENGINE_TOOLS = Path(__file__).resolve().parent
ENGINE = ENGINE_TOOLS.parent
ROOT = ENGINE.parent  # CAmod (engine/, mods/, OpenRA.Mods.CA/, …)

# Chrome / ingame UI: yaml + mod json5 still reference these legacy keys.
EXTRA_REPLACEMENTS: list[tuple[str, str]] = [
    ("button-command-bar-queue-orders.tooltipdesc", "Chrome-InGamePlayer-CommandBar-QueueOrders-Desc"),
    ("button-command-bar-queue-orders.tooltip", "Chrome-InGamePlayer-CommandBar-QueueOrders-Tooltip"),
    ("button-command-bar-scatter.tooltipdesc", "Chrome-InGamePlayer-CommandBar-Scatter-Desc"),
    ("button-command-bar-scatter.tooltip", "Chrome-InGamePlayer-CommandBar-Scatter-Tooltip"),
    ("button-command-bar-stop.tooltipdesc", "Chrome-InGamePlayer-CommandBar-Stop-Desc"),
    ("button-command-bar-stop.tooltip", "Chrome-InGamePlayer-CommandBar-Stop-Tooltip"),
    ("button-command-bar-deploy.tooltipdesc", "Chrome-InGamePlayer-CommandBar-Deploy-Desc"),
    ("button-command-bar-deploy.tooltip", "Chrome-InGamePlayer-CommandBar-Deploy-Tooltip"),
    ("button-command-bar-guard.tooltipdesc", "Chrome-InGamePlayer-CommandBar-Guard-Desc"),
    ("button-command-bar-guard.tooltip", "Chrome-InGamePlayer-CommandBar-Guard-Tooltip"),
    ("button-command-bar-force-attack.tooltipdesc", "Chrome-InGamePlayer-CommandBar-ForceAttack-Desc"),
    ("button-command-bar-force-attack.tooltip", "Chrome-InGamePlayer-CommandBar-ForceAttack-Tooltip"),
    ("button-command-bar-force-move.tooltipdesc", "Chrome-InGamePlayer-CommandBar-ForceMove-Desc"),
    ("button-command-bar-force-move.tooltip", "Chrome-InGamePlayer-CommandBar-ForceMove-Tooltip"),
    ("button-command-bar-attack-move.tooltipdesc", "Chrome-InGamePlayer-CommandBar-AttackMove-Desc"),
    ("button-command-bar-attack-move.tooltip", "Chrome-InGamePlayer-CommandBar-AttackMove-Tooltip"),
    ("button-stance-bar-attackanything.tooltipdesc", "Chrome-InGamePlayer-StanceBar-AttackAnything-Desc"),
    ("button-stance-bar-attackanything.tooltip", "Chrome-InGamePlayer-StanceBar-AttackAnything-Tooltip"),
    ("button-stance-bar-returnfire.tooltipdesc", "Chrome-InGamePlayer-StanceBar-ReturnFire-Desc"),
    ("button-stance-bar-returnfire.tooltip", "Chrome-InGamePlayer-StanceBar-ReturnFire-Tooltip"),
    ("button-stance-bar-holdfire.tooltipdesc", "Chrome-InGamePlayer-StanceBar-HoldFire-Desc"),
    ("button-stance-bar-holdfire.tooltip", "Chrome-InGamePlayer-StanceBar-HoldFire-Tooltip"),
    ("button-stance-bar-defend.tooltipdesc", "Chrome-InGamePlayer-StanceBar-Defend-Desc"),
    ("button-stance-bar-defend.tooltip", "Chrome-InGamePlayer-StanceBar-Defend-Tooltip"),
    ("button-top-buttons-options-tooltip", "Chrome-InGamePlayer-OrderOptionsTooltip"),
    ("button-top-buttons-beacon-tooltip", "Chrome-InGamePlayer-OrderBeaconTooltip"),
    ("button-top-buttons-sell-tooltip", "Chrome-InGamePlayer-OrderSellTooltip"),
    ("button-top-buttons-power-tooltip", "Chrome-InGamePlayer-OrderPowerDownTooltip"),
    ("button-top-buttons-repair-tooltip", "Chrome-InGamePlayer-OrderRepairTooltip"),
    ("button-production-types-scroll-down-tooltip", "Chrome-InGamePlayer-ProductionScrollDownTooltip"),
    ("button-production-types-scroll-up-tooltip", "Chrome-InGamePlayer-ProductionScrollUpTooltip"),
    ("button-production-types-naval-tooltip", "Chrome-InGamePlayer-ProductionType-Naval"),
    ("button-production-types-defense-tooltip", "Chrome-InGamePlayer-ProductionType-Defense"),
    ("button-production-types-support-tooltip", "Chrome-InGamePlayer-ProductionType-Support"),
    ("button-production-types-aircraft-tooltip", "Chrome-InGamePlayer-ProductionType-Aircraft"),
    ("button-production-types-vehicle-tooltip", "Chrome-InGamePlayer-ProductionType-Vehicles"),
    ("button-production-types-infantry-tooltip", "Chrome-InGamePlayer-ProductionType-Infantry"),
    ("button-production-types-building-tooltip", "Chrome-InGamePlayer-ProductionType-Building"),
    ("button-production-types-upgrade-tooltip", "Chrome-InGamePlayer-ProductionType-Upgrades"),
    ("button-production-types-starport-tooltip", "Chrome-InGamePlayer-ProductionType-Starport"),
    ("button-production-types-tanks-tooltip", "Chrome-InGamePlayer-ProductionType-Tanks"),
    ("button-observer-widgets-maximum.tooltip", "Chrome-InGameObserver-MaxButton-Tooltip"),
    ("button-observer-widgets-maximum.label", "Chrome-InGameObserver-MaxButton"),
    ("button-observer-widgets-fast.tooltip", "Chrome-InGameObserver-200Button-Tooltip"),
    ("button-observer-widgets-fast.label", "Chrome-InGameObserver-200Button-Label"),
    ("button-observer-widgets-regular.tooltip", "Chrome-InGameObserver-100Button-Tooltip"),
    ("button-observer-widgets-regular.label", "Chrome-InGameObserver-100Button-Label"),
    ("button-observer-widgets-slow.tooltip", "Chrome-InGameObserver-50Button-Tooltip"),
    ("button-observer-widgets-slow.label", "Chrome-InGameObserver-50Button-Label"),
    ("button-observer-widgets-play-tooltip", "Chrome-InGameObserver-PlayButton-Tooltip"),
    ("button-observer-widgets-pause-tooltip", "Chrome-InGameObserver-PauseButton-Tooltip"),
    ("button-replay-player-maximum.tooltip", "Chrome-InGameObserver-MaxButton-Tooltip"),
    ("button-replay-player-maximum.label", "Chrome-InGameObserver-MaxButton"),
    ("button-replay-player-fast.tooltip", "Chrome-InGameObserver-200Button-Tooltip"),
    ("button-replay-player-fast.label", "Chrome-InGameObserver-200Button-Label"),
    ("button-replay-player-regular.tooltip", "Chrome-InGameObserver-100Button-Tooltip"),
    ("button-replay-player-regular.label", "Chrome-InGameObserver-100Button-Label"),
    ("button-replay-player-slow.tooltip", "Chrome-InGameObserver-50Button-Tooltip"),
    ("button-replay-player-slow.label", "Chrome-InGameObserver-50Button-Label"),
    ("button-replay-player-play-tooltip", "Chrome-InGameObserver-PlayButton-Tooltip"),
    ("button-replay-player-pause-tooltip", "Chrome-InGameObserver-PauseButton-Tooltip"),
    ("button-hotkey-remap-dialog-reset.tooltip", "Chrome-Setting-Hotkey-Reset-Tooltip"),
    ("button-hotkey-remap-dialog-reset.label", "Chrome-Setting-Hotkey-Reset"),
    ("button-hotkey-remap-dialog-clear.tooltip", "Chrome-Setting-Hotkey-Clear-Tooltip"),
    ("button-hotkey-remap-dialog-clear.label", "Chrome-Setting-Hotkey-Clear"),
    ("button-hotkey-remap-dialog-override", "Chrome-Setting-Hotkey-OverrideButton"),
    ("button-observer-widget-options-tooltip", "Chrome-Editor-MenuButton-Tooltip"),
    ("button-observer-widget-options", "Chrome-InGameObserver-Options"),
    ("button-map-editor-tab-container-history-tooltip", "Chrome-Editor-TabContainer-HistoryTooltip"),
    ("button-map-editor-tab-container-tools-tooltip", "Chrome-Editor-TabContainer-ToolsTooltip"),
    ("button-map-editor-tab-container-actors-tooltip", "Chrome-Editor-TabContainer-ActorsTooltip"),
    ("button-map-editor-tab-container-overlays-tooltip", "Chrome-Editor-TabContainer-OverlaysTooltip"),
    ("button-map-editor-tab-container-tiles-tooltip", "Chrome-Editor-TabContainer-TilesTooltip"),
    ("button-map-editor-tab-container-select-tooltip", "Chrome-Editor-TabContainer-SelectionTooltip"),
    ("button-editor-world-root-paste.tooltip", "Chrome-Editor-WorldRoot-PasteTooltip"),
    ("button-editor-world-root-paste.label", "Chrome-Editor-WorldRoot-PasteLabel"),
    ("button-editor-world-root-copy.tooltip", "Chrome-Editor-WorldRoot-CopyTooltip"),
    ("button-editor-world-root-copy.label", "Chrome-Editor-WorldRoot-CopyLabel"),
    ("button-editor-world-root-redo.tooltip", "Chrome-Editor-RedoButton-Tooltip"),
    ("button-editor-world-root-redo.label", "Chrome-Editor-RedoButton"),
    ("button-editor-world-root-undo.tooltip", "Chrome-Editor-UndoButton-Tooltip"),
    ("button-editor-world-root-undo.label", "Chrome-Editor-UndoButton"),
    ("button-editor-world-root-options.tooltip", "Chrome-Editor-MenuButton-Tooltip"),
    ("button-editor-world-root-options.label", "Chrome-Editor-MenuButton"),
    ("button-delete-area.tooltip", "Chrome-Editor-DeleteArea-Tooltip"),
    ("button-delete-area.label", "Chrome-Editor-DeleteButton"),
    ("button-delete-actor.tooltip", "Chrome-Editor-DeleteActor-Tooltip"),
    ("button-delete-actor.label", "Chrome-Editor-DeleteButton"),
    ("dropdownbutton-editor-world-root-overlay-button", "Chrome-Editor-WorldRoot-OverlayDropdown"),
]

_JSON5_KEY_LINE = re.compile(r'^[\t ]*"([^"]+)":')

EXTRA_OLD_KEYS = frozenset(old for old, _ in EXTRA_REPLACEMENTS)


def load_replacements() -> list[tuple[str, str]]:
    mtk = ENGINE_TOOLS / "migrate_translation_keys.py"
    spec = importlib.util.spec_from_file_location("_mtk", mtk)
    mod = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    spec.loader.exec_module(mod)
    rep = list(mod.REPLACEMENTS) + list(EXTRA_REPLACEMENTS)
    rep.sort(key=lambda x: len(x[0]), reverse=True)
    return rep


def migrate_json5_remove_extra_keys(path: Path) -> bool:
    try:
        text = path.read_text(encoding="utf-8")
    except OSError:
        return False
    lines = text.splitlines(keepends=True)
    out: list[str] = []
    changed = False
    for line in lines:
        m = _JSON5_KEY_LINE.match(line)
        if m and m.group(1) in EXTRA_OLD_KEYS:
            changed = True
            continue
        out.append(line)
    if not changed:
        return False
    path.write_text("".join(out), encoding="utf-8")
    return True


def migrate_text_replace(path: Path, replacements: list[tuple[str, str]]) -> bool:
    try:
        text = path.read_text(encoding="utf-8")
    except OSError:
        return False
    orig = text
    for old, new in replacements:
        if old in text:
            text = text.replace(old, new)
    if text == orig:
        return False
    path.write_text(text, encoding="utf-8")
    return True


def main() -> int:
    rep = load_replacements()
    exts = {".json5", ".yaml", ".yml", ".cs", ".lua"}
    skip = {"bin", "obj", ".git"}
    changed = 0
    if not ROOT.is_dir():
        print("missing", ROOT, file=sys.stderr)
        return 1
    for path in ROOT.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in exts:
            continue
        if any(p in skip for p in path.parts):
            continue
        if path.suffix.lower() == ".json5":
            if migrate_json5_remove_extra_keys(path):
                changed += 1
                print(path.relative_to(ROOT))
        elif migrate_text_replace(path, rep):
            changed += 1
            print(path.relative_to(ROOT))
    print("files changed:", changed)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
