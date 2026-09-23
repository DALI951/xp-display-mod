# UI Data Gathering Report — Supermarket Simulator v1.6.0 + XPDisplayMod

Date: 2026-09-24. Purpose: collect the native settings-screen UI data (Image/TMP/
Toggle/Slider internals) + native XP/level node locations so the next session can
clone REAL game UI instead of hand-drawn IMGUI. **No panel/toggle/slider clone code
was written — this was a data-gathering pass only.**

## Raw data files (this folder)

| File | Contents |
|---|---|
| `RunA-main-menu-settings.txt` (121 KB) | FULL one-shot dump of the MAIN MENU settings screen: settings tree (depth 7 via DumpTransform), Buttons list, Sliders list, Toggles list, node inspector (Image/TMP/Toggle/Slider/component details + children) for fixed names + every matched-name node (contains Toggle/Slider/Checkbox/Panel/Background, case-insensitive). 1 SettingsMenuManager instance. |
| `RunB-native-xp-scan.txt` (25 KB) | One-shot native scan over ALL 6070 RectTransforms: every node whose name contains xp/level/exp/progress/store (case-insensitive), with parent + full path. Includes the native XP bar find. |
| `RunB-settings-instances.txt` (25 KB) | Run B settings screen state: instances discovery + native-XP scan tail. |

## Verified facts (from logs + Dali's manual testing)

1. **In-game settings = TWO `SettingsMenuManager` instances**, both under
   `---UI---/PopUps/Escape Menu/`:
   - `[0] XboxSettingMenu` — the screen the player actually SEES (Xbox-style UI)
   - `[1] NewSettings Menu` — PC-style screen (same shape as the main-menu one)
   Both reported `active=True` while the in-game settings is open.
   Main-menu settings = single `NewSettings Menu` (parent=none).
2. **Every text in the game UI is TMP** — the tree is full of `Text (TMP)` nodes.
   `Unity.TextMeshPro` reference was ADDED to the csproj (HintPath → game interop
   directory). TMPro namespace = `TMPro`, type = `TMPro.TextMeshProUGUI`.
3. **THE native XP bar**: `Store Point Slider` at
   `---UI---/Ingame Canvas/Store Point Slider` (parent = `Ingame Canvas`).
   Day-end report: `Store Point Title` / `Store Point` / `Store Point Text` under
   `---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/`.
   Other candidates: `Reqired Level Text` (game's typo!) in hiring panels,
   `SelectStoreLevel` / `MaxStoreLevel` / `LevelInputField` in `CheatManager`
   (dev tools — useful reference controller), `Progress` nodes = keybind-progress
   noise, `Store Name Input`, `Expenses`/`Dirty Store`/`OnlineOrder Expired` = noise.
4. **Build pipeline works**: 4 builds this session, all EXIT=0 with 0 error CS
   (framework MSBuild 4.0 + Roslyn CscToolPath override; known benign warnings
   MSB3644 + MSB3270). Final deployed DLL = 39,936 B.
5. **Dali tested the IMGUI ModsCfgPanel (F2) in-game**: toggles + sliders +
   color rows all interact fine. That part is verified working.

## What is NOT solved (read carefully — the point of this pass)

6. **The in-game settings dump NEVER fires.** Three attempts, three different gate
   variants — `DumpSettingsScreen()` is one-shot gated on `openNow`, and no variant
   ever saw `openNow == true` for the in-game Escape Menu settings:
   - v1 gate: `FindChild(_settingsMenu, "Menu").activeInHierarchy` → cached instance
     was the wrong one (main-menu `NewSettings Menu`, inactive in-game) → never true.
   - v2 gate (instance tracking): scan now re-picks the ACTIVE instance each rescan
     (prefer instance with active `Menu` child, else active root, else found[0]).
   - v3 gate (root fallback): `openNow` = active `Menu` child OR tracked instance
     root active-in-hierarchy; injection/EnforceStrip/HitTest ONLY when `Menu` child
     exists (so no garbage injection into the Xbox UI).
   Even with v3, the tracked instance (`XboxSettingMenu`, logged `active=True`)
   produced NO dump on the same tick or any later tick. Log stops at the instance
   detail line; subsequent 1s scans run silently. `[XP] scan failed:` /
   `dump failed:` NEVER appeared → no exception was thrown; the gate just stayed
   false. **Root cause UNRESOLVED — likely the Escape Menu settings screens toggle
   visibility through some mechanism invisible to `activeInHierarchy` sampling
   (e.g. Canvas.enabled / gameObject.activeSelf on a parent, or they are only
   "open" for part of the tick).** This is an UNSURE — next session should
   instrument differently (see next steps).
7. **Consequence: the XPDisplayTabButton injection also never runs in-game**
   (injection is gated on the same `openNow` + a `Menu`-child layout). Dali
   confirmed: in-game settings shows NO XP Display tab. Main-menu settings was
   never visually verified this session (dump ran there, injection targets the
   PC-layout there and should work — pending visual check).

## Next session — concrete next steps

1. Debug the open-gate: instead of sampling `activeInHierarchy`, log EVERY scan tick
   while `SettingsMenuManager` instances exist — print each instance's root active,
   `FindChild(instance, "Menu")` result + its active state, and `activeSelf`. Watch
   what changes when Dali opens in-game settings. (Or hook the settings open — e.g.
   patch `SettingsMenuManager`/the escape-menu button onClick, or scan
   `FindObjectsOfType<Canvas>(true)` for one whose `enabled` JUST became true.)
2. Do NOT inject the tab into `XboxSettingMenu` until its layout is dumped — its
   button strip may differ from the PC `NewSettings Menu` layout (`Buttons` host +
   `*Tab Button` children used by `FindTabHost`).
3. Once the in-game dump fires: reuse the SAME dump code paths
   (`DumpNodeTransform` with Image/TMP/Toggle/Slider detail lines) — they already
   exist and produced the full Run A payload.
4. Clone targets identified: `Store Point Slider` (XP bar) + `Store Point Text`;
   hiring `Reqired Level Text` for lock indicators.

## Code state (committed with this report)

- `XPDisplayBehaviour.cs`: `DumpNode` → `DumpNodeTransform(Transform, string)`
  overload (Image/TMP/Toggle/Slider/component-list detail logging, each in own
  try/catch, enum-compare over ToString for stripped types); `DumpSettingsScreen`
  added Sliders list + Toggles list + matched-name node dumps; NEW one-shot
  `ScanNativeXpNodes()` gated by `_nativeXpScanDone`, fired from
  `OnManagerAcquired()`; scan now tracks the ACTIVE settings instance (prefer
  active `Menu` child → active root → found[0]); `openNow` falls back to the
  tracked root's activeInHierarchy; injection stays PC-layout-only.
- `XPDisplayMod.csproj`: added `Unity.TextMeshPro` reference.
- `ModsCfgPanel.cs`: untouched this pass (IMGUI panel, verified in-game by Dali).