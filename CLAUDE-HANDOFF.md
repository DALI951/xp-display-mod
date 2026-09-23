## 1. UI-DATA-REPORT.md (full contents)

# UI Data Gathering Report â€” Supermarket Simulator v1.6.0 + XPDisplayMod

Date: 2026-09-24. Purpose: collect the native settings-screen UI data (Image/TMP/
Toggle/Slider internals) + native XP/level node locations so the next session can
clone REAL game UI instead of hand-drawn IMGUI. **No panel/toggle/slider clone code
was written â€” this was a data-gathering pass only.**

## Raw data files (this folder)

| File | Contents |
|---|---|
| `RunA-main-menu-settings.txt` (121 KB) | FULL one-shot dump of the MAIN MENU settings screen: settings tree (depth 7 via DumpTransform), Buttons list, Sliders list, Toggles list, node inspector (Image/TMP/Toggle/Slider/component details + children) for fixed names + every matched-name node (contains Toggle/Slider/Checkbox/Panel/Background, case-insensitive). 1 SettingsMenuManager instance. |
| `RunB-native-xp-scan.txt` (25 KB) | One-shot native scan over ALL 6070 RectTransforms: every node whose name contains xp/level/exp/progress/store (case-insensitive), with parent + full path. Includes the native XP bar find. |
| `RunB-settings-instances.txt` (25 KB) | Run B settings screen state: instances discovery + native-XP scan tail. |

## Verified facts (from logs + Dali's manual testing)

1. **In-game settings = TWO `SettingsMenuManager` instances**, both under
   `---UI---/PopUps/Escape Menu/`:
   - `[0] XboxSettingMenu` â€” the screen the player actually SEES (Xbox-style UI)
   - `[1] NewSettings Menu` â€” PC-style screen (same shape as the main-menu one)
   Both reported `active=True` while the in-game settings is open.
   Main-menu settings = single `NewSettings Menu` (parent=none).
2. **Every text in the game UI is TMP** â€” the tree is full of `Text (TMP)` nodes.
   `Unity.TextMeshPro` reference was ADDED to the csproj (HintPath â†’ game interop
   directory). TMPro namespace = `TMPro`, type = `TMPro.TextMeshProUGUI`.
3. **THE native XP bar**: `Store Point Slider` at
   `---UI---/Ingame Canvas/Store Point Slider` (parent = `Ingame Canvas`).
   Day-end report: `Store Point Title` / `Store Point` / `Store Point Text` under
   `---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/`.
   Other candidates: `Reqired Level Text` (game's typo!) in hiring panels,
   `SelectStoreLevel` / `MaxStoreLevel` / `LevelInputField` in `CheatManager`
   (dev tools â€” useful reference controller), `Progress` nodes = keybind-progress
   noise, `Store Name Input`, `Expenses`/`Dirty Store`/`OnlineOrder Expired` = noise.
4. **Build pipeline works**: 4 builds this session, all EXIT=0 with 0 error CS
   (framework MSBuild 4.0 + Roslyn CscToolPath override; known benign warnings
   MSB3644 + MSB3270). Final deployed DLL = 39,936 B.
5. **Dali tested the IMGUI ModsCfgPanel (F2) in-game**: toggles + sliders +
   color rows all interact fine. That part is verified working.

## What is NOT solved (read carefully â€” the point of this pass)

6. **The in-game settings dump NEVER fires.** Three attempts, three different gate
   variants â€” `DumpSettingsScreen()` is one-shot gated on `openNow`, and no variant
   ever saw `openNow == true` for the in-game Escape Menu settings:
   - v1 gate: `FindChild(_settingsMenu, "Menu").activeInHierarchy` â†’ cached instance
     was the wrong one (main-menu `NewSettings Menu`, inactive in-game) â†’ never true.
   - v2 gate (instance tracking): scan now re-picks the ACTIVE instance each rescan
     (prefer instance with active `Menu` child, else active root, else found[0]).
   - v3 gate (root fallback): `openNow` = active `Menu` child OR tracked instance
     root active-in-hierarchy; injection/EnforceStrip/HitTest ONLY when `Menu` child
     exists (so no garbage injection into the Xbox UI).
   Even with v3, the tracked instance (`XboxSettingMenu`, logged `active=True`)
   produced NO dump on the same tick or any later tick. Log stops at the instance
   detail line; subsequent 1s scans run silently. `[XP] scan failed:` /
   `dump failed:` NEVER appeared â†’ no exception was thrown; the gate just stayed
   false. **Root cause UNRESOLVED â€” likely the Escape Menu settings screens toggle
   visibility through some mechanism invisible to `activeInHierarchy` sampling
   (e.g. Canvas.enabled / gameObject.activeSelf on a parent, or they are only
   "open" for part of the tick).** This is an UNSURE â€” next session should
   instrument differently (see next steps).
7. **Consequence: the XPDisplayTabButton injection also never runs in-game**
   (injection is gated on the same `openNow` + a `Menu`-child layout). Dali
   confirmed: in-game settings shows NO XP Display tab. Main-menu settings was
   never visually verified this session (dump ran there, injection targets the
   PC-layout there and should work â€” pending visual check).

## Next session â€” concrete next steps

1. Debug the open-gate: instead of sampling `activeInHierarchy`, log EVERY scan tick
   while `SettingsMenuManager` instances exist â€” print each instance's root active,
   `FindChild(instance, "Menu")` result + its active state, and `activeSelf`. Watch
   what changes when Dali opens in-game settings. (Or hook the settings open â€” e.g.
   patch `SettingsMenuManager`/the escape-menu button onClick, or scan
   `FindObjectsOfType<Canvas>(true)` for one whose `enabled` JUST became true.)
2. Do NOT inject the tab into `XboxSettingMenu` until its layout is dumped â€” its
   button strip may differ from the PC `NewSettings Menu` layout (`Buttons` host +
   `*Tab Button` children used by `FindTabHost`).
3. Once the in-game dump fires: reuse the SAME dump code paths
   (`DumpNodeTransform` with Image/TMP/Toggle/Slider detail lines) â€” they already
   exist and produced the full Run A payload.
4. Clone targets identified: `Store Point Slider` (XP bar) + `Store Point Text`;
   hiring `Reqired Level Text` for lock indicators.

## Code state (committed with this report)

- `XPDisplayBehaviour.cs`: `DumpNode` â†’ `DumpNodeTransform(Transform, string)`
  overload (Image/TMP/Toggle/Slider/component-list detail logging, each in own
  try/catch, enum-compare over ToString for stripped types); `DumpSettingsScreen`
  added Sliders list + Toggles list + matched-name node dumps; NEW one-shot
  `ScanNativeXpNodes()` gated by `_nativeXpScanDone`, fired from
  `OnManagerAcquired()`; scan now tracks the ACTIVE settings instance (prefer
  active `Menu` child â†’ active root â†’ found[0]); `openNow` falls back to the
  tracked root's activeInHierarchy; injection stays PC-layout-only.
- `XPDisplayMod.csproj`: added `Unity.TextMeshPro` reference.
- `ModsCfgPanel.cs`: untouched this pass (IMGUI panel, verified in-game by Dali).

## 2. RunB-settings-instances.txt (full contents)

[Message:   BepInEx] Chainloader startup complete
[Info   :XP Display Mod] [XP] rect inventory size: 21
[Info   :XP Display Mod] [XP] SettingsMenuManager instances: 1 tracking=NewSettings Menu
[Info   :XP Display Mod] [XP]   [0] root=NewSettings Menu active=True parent=none
[Info   :XP Display Mod] [XP] native-XP scan: 6070 rects
[Info   :XP Display Mod] [XP] native-XP candidate: 'Expenses Canvas' parent=---UI--- path=---UI---/Expenses Canvas
[Info   :XP Display Mod] [XP] native-XP candidate: 'Expenses Warning Canvas' parent=Warning Screen path=---UI---/PopUps/Warning Screen/Expenses Warning Canvas
[Info   :XP Display Mod] [XP] native-XP candidate: 'Expenses' parent=BG path=---UI---/Expenses Canvas/BG/Expenses
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Point Slider' parent=Ingame Canvas path=---UI---/Ingame Canvas/Store Point Slider
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/Keyboard_Player_Interaction_GridToggle/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/Keyboard_Player_Interaction_GridSizePrev/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Refuel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Left/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'SelectStoreLevel' parent=ButtonParent path=CheatManager/Panel/ButtonParent/SelectStoreLevel
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Drop/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Exit/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item (2)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'LevelInputField' parent=LevelFieldParent path=CheatManager/Panel/InputFieldParent/LevelFieldParent/LevelInputField
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/GuardsView/Viewport/GuardsPanel/Security Guard Item/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'LevelFieldParent' parent=InputFieldParent path=CheatManager/Panel/InputFieldParent/LevelFieldParent
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Forward/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Handbrake/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/Keyboard_Player_Interaction_GridSizeNext/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Toolwheel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Products Found Expensive Text' parent=Products Found Expensive path=---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/Products Found Expensive/Products Found Expensive Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Point Text' parent=Store Point path=---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/Store Point/Store Point Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Products Found Expensive' parent=Daily Statistics Screen path=---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/Products Found Expensive
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Point' parent=Daily Statistics Screen path=---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/Store Point
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Reset/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/Keyboard_Player_Interaction_SnapPlace/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Back/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Map/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item (5)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Dirty Store Notification' parent=Warning Canvas path=---UI---/Warning Canvas/Dirty Store Notification
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/GuardsView/Viewport/GuardsPanel/Security Guard Item (1)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Throw/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Quicksave/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Forward/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Handbrake/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item (3)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/IceCreamHelpersView/Viewport/IceCreamHelperPanel/IceCreamHelperItem/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Name Input' parent=InputFieldParent path=---UI---/PopUps/NameChangeCanvas/Window/InteractableParent/InputFieldParent/Store Name Input
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Drop/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Exit/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/Keyboard_Player_Interaction_GridToggle/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/Keyboard_Player_Interaction_GridSizePrev/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Refuel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Left/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/JanitorsView/Viewport/JanitorsPanel/Janitor Item (1)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Throw/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Quicksave/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Back/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Map/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Name UI' parent=Content path=---GAME---/Computer &&/Screen/Management App/Tabs/CustomizationTab/Unlocked/Sections Scroll View/Viewport/Content/Store Name UI
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/IceCreamHelpersView/Viewport/IceCreamHelperPanel/IceCreamHelperItem (1)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Reset/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item (5)/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item (4)/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/Keyboard_Player_Interaction_SnapPlace/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/Keyboard_Player_Interaction_GridSizeNext/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Name Store Mission' parent=Mission Canvas path=---UI---/Leftside Layout/Vertical Layout/Mission Canvas/Name Store Mission
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Point Title' parent=Daily Statistics Screen path=---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/Store Point Title
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Toolwheel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Steer_Left/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Brake/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/Keyboard_Player_Interaction_SnapRotate/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Take/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Wholesale/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Backward/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Use/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'MaxStoreLevel' parent=ButtonParent path=CheatManager/Panel/ButtonParent/MaxStoreLevel
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Sell/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Sprint/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item (6)/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Steer_Right/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Right/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Gas/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item (2)/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item (2)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Open Store' parent=Onboarding Canvas path=---MANAGERS---/Onboarding/Onboarding Canvas/Open Store
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item (3)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_OnlineOrder/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Jump/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Cancel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Sprint/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item (6)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Steer_Right/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Right/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Gas/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/JanitorsView/Viewport/JanitorsPanel/Janitor Item (2)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'OnlineOrder Expired Notification' parent=Warning Canvas path=---UI---/Warning Canvas/OnlineOrder Expired Notification
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Wholesale/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item (5)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Backward/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/JanitorsView/Viewport/JanitorsPanel/Janitor Item/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Use/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Sell/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Steer_Left/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Brake/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item (3)/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Take/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/Keyboard_Player_Interaction_SnapRotate/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_OnlineOrder/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Jump/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item (4)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item (1)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Cancel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item (4)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP scan done
[Info   :XP Display Mod] [XP] SettingsMenuManager instances: 2 tracking=XboxSettingMenu
[Info   :XP Display Mod] [XP]   [0] root=XboxSettingMenu active=True parent=Escape Menu
[Info   :XP Display Mod] [XP]   [1] root=NewSettings Menu active=True parent=Escape Menu


## 3. RunB-native-xp-scan.txt (full contents)

[Info   :XP Display Mod] [XP]   [0] root=NewSettings Menu active=True parent=none
[Info   :XP Display Mod] [XP] native-XP scan: 6070 rects
[Info   :XP Display Mod] [XP] native-XP candidate: 'Expenses Canvas' parent=---UI--- path=---UI---/Expenses Canvas
[Info   :XP Display Mod] [XP] native-XP candidate: 'Expenses Warning Canvas' parent=Warning Screen path=---UI---/PopUps/Warning Screen/Expenses Warning Canvas
[Info   :XP Display Mod] [XP] native-XP candidate: 'Expenses' parent=BG path=---UI---/Expenses Canvas/BG/Expenses
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Point Slider' parent=Ingame Canvas path=---UI---/Ingame Canvas/Store Point Slider
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/Keyboard_Player_Interaction_GridToggle/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/Keyboard_Player_Interaction_GridSizePrev/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Refuel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Left/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'SelectStoreLevel' parent=ButtonParent path=CheatManager/Panel/ButtonParent/SelectStoreLevel
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Drop/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Exit/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item (2)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'LevelInputField' parent=LevelFieldParent path=CheatManager/Panel/InputFieldParent/LevelFieldParent/LevelInputField
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/GuardsView/Viewport/GuardsPanel/Security Guard Item/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'LevelFieldParent' parent=InputFieldParent path=CheatManager/Panel/InputFieldParent/LevelFieldParent
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Forward/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Handbrake/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/Keyboard_Player_Interaction_GridSizeNext/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Toolwheel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Products Found Expensive Text' parent=Products Found Expensive path=---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/Products Found Expensive/Products Found Expensive Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Point Text' parent=Store Point path=---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/Store Point/Store Point Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Products Found Expensive' parent=Daily Statistics Screen path=---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/Products Found Expensive
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Point' parent=Daily Statistics Screen path=---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/Store Point
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Reset/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/Keyboard_Player_Interaction_SnapPlace/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Back/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Map/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item (5)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Dirty Store Notification' parent=Warning Canvas path=---UI---/Warning Canvas/Dirty Store Notification
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/GuardsView/Viewport/GuardsPanel/Security Guard Item (1)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Throw/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Quicksave/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Forward/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Handbrake/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item (3)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/IceCreamHelpersView/Viewport/IceCreamHelperPanel/IceCreamHelperItem/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Name Input' parent=InputFieldParent path=---UI---/PopUps/NameChangeCanvas/Window/InteractableParent/InputFieldParent/Store Name Input
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Drop/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Exit/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/Keyboard_Player_Interaction_GridToggle/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/Keyboard_Player_Interaction_GridSizePrev/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Refuel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Left/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/JanitorsView/Viewport/JanitorsPanel/Janitor Item (1)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Throw/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Quicksave/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Back/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Map/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Name UI' parent=Content path=---GAME---/Computer &&/Screen/Management App/Tabs/CustomizationTab/Unlocked/Sections Scroll View/Viewport/Content/Store Name UI
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/IceCreamHelpersView/Viewport/IceCreamHelperPanel/IceCreamHelperItem (1)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Reset/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item (5)/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item (4)/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/Keyboard_Player_Interaction_SnapPlace/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/Keyboard_Player_Interaction_GridSizeNext/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Name Store Mission' parent=Mission Canvas path=---UI---/Leftside Layout/Vertical Layout/Mission Canvas/Name Store Mission
[Info   :XP Display Mod] [XP] native-XP candidate: 'Store Point Title' parent=Daily Statistics Screen path=---UI---/PopUps/Day Cycle Canvas/Daily Statistics Screen/Store Point Title
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Toolwheel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Steer_Left/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Brake/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/Keyboard_Player_Interaction_SnapRotate/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Take/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Wholesale/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Backward/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Use/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'MaxStoreLevel' parent=ButtonParent path=CheatManager/Panel/ButtonParent/MaxStoreLevel
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Sell/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Sprint/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item (6)/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Steer_Right/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Right/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Gas/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item (2)/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item (2)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Open Store' parent=Onboarding Canvas path=---MANAGERS---/Onboarding/Onboarding Canvas/Open Store
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item (3)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_OnlineOrder/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Jump/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Cancel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Sprint/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item (6)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Steer_Right/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Right/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Gas/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/JanitorsView/Viewport/JanitorsPanel/Janitor Item (2)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'OnlineOrder Expired Notification' parent=Warning Canvas path=---UI---/Warning Canvas/OnlineOrder Expired Notification
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Wholesale/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item (5)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Backward/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/JanitorsView/Viewport/JanitorsPanel/Janitor Item/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Use/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Sell/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Steer_Left/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Brake/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/RestockerView/Viewport/RestockerPanel/Restocker Item (3)/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Take/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/Keyboard_Player_Interaction_SnapRotate/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_OnlineOrder/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/KeyboardEditor/Viewport/Content/KeybindEntry_Jump/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item (4)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/HelperView/Viewport/HelperPanel/CustomerHelper Item (1)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP candidate: 'Progress' parent=RebindButton path=---UI---/PopUps/Escape Menu/NewSettings Menu/Menu/Window BG/Tabs/Keybinding Tab/GamepadEditor/Viewport/Content/KeybindEntry_Cancel/RebindButton/Progress
[Info   :XP Display Mod] [XP] native-XP candidate: 'Reqired Level Text' parent=Requirements path=---GAME---/Computer &&/Screen/Management App/Tabs/Hiring Tab/Panels/CashierView/Viewport/CashierPanel/Cashier Item (4)/Elements/Info/Requirements/Reqired Level Text
[Info   :XP Display Mod] [XP] native-XP scan done


## 4. RunA-main-menu-settings.txt (filtered)

[Info   :XP Display Mod]           Save Button
[Info   :XP Display Mod]           Back Button
[Info   :XP Display Mod]           Save Button
[Info   :XP Display Mod]           Back Button
[Info   :XP Display Mod]               HDR Toggle
[Info   :XP Display Mod]               PostProcessToggle
[Info   :XP Display Mod]             Save Button
[Info   :XP Display Mod]             Back Button
[Info   :XP Display Mod]           Save Button
[Info   :XP Display Mod]           Back Button
[Info   :XP Display Mod]           Save Button
[Info   :XP Display Mod]           Back Button
[Info   :XP Display Mod]           Save Button
[Info   :XP Display Mod]           Back Button
[Info   :XP Display Mod]   [5] Save Button  text=''  parent=Gameplay Tab
[Info   :XP Display Mod]   [6] Back Button  text=''  parent=Gameplay Tab
[Info   :XP Display Mod]   [8] Save Button  text=''  parent=Graphics Tab
[Info   :XP Display Mod]   [9] Back Button  text=''  parent=Graphics Tab
[Info   :XP Display Mod]   [10] Save Button  text=''  parent=AdvancedGraphics
[Info   :XP Display Mod]   [11] Back Button  text=''  parent=AdvancedGraphics
[Info   :XP Display Mod]   [12] Save Button  text=''  parent=Audio Tab
[Info   :XP Display Mod]   [13] Back Button  text=''  parent=Audio Tab
[Info   :XP Display Mod]   [27] Save Button  text=''  parent=Interface Tab
[Info   :XP Display Mod]   [28] Back Button  text=''  parent=Interface Tab
[Info   :XP Display Mod]   [57] RebindButton  text=''  parent=Keyboard_Player_Interaction_GridToggle
[Info   :XP Display Mod]   [58] ResetButton  text=''  parent=Keyboard_Player_Interaction_GridToggle
[Info   :XP Display Mod]   [119] RebindButton  text=''  parent=Keyboard_Player_Interaction_GridToggle
[Info   :XP Display Mod]   [120] ResetButton  text=''  parent=Keyboard_Player_Interaction_GridToggle
[Info   :XP Display Mod]   [155] Save Button  text=''  parent=Keybinding Tab
[Info   :XP Display Mod]   [156] Back Button  text=''  parent=Keybinding Tab
[Info   :XP Display Mod] === [XP] Sliders: 9
[Info   :XP Display Mod]   [3] FOV Setting Slider path=NewSettings Menu/Menu/Window BG/Tabs/Graphics Tab/Scroll/Viewport/Content/FOV/FOV Setting Slider
[Info   :XP Display Mod]   [7] Scale Slider path=NewSettings Menu/Menu/Window BG/Tabs/Interface Tab/PanelParent/CrosshairPanel/Scroll/Viewport/Content/Crosshair/Scale Panel/Scale Slider
[Info   :XP Display Mod]   [8] AlphaSlider path=NewSettings Menu/Menu/Window BG/Tabs/Interface Tab/PanelParent/GridPanel/Scroll/Viewport/Content/GridColor/AlphaPanel/AlphaSlider
[Info   :XP Display Mod] === [XP] Toggles: 22
[Info   :XP Display Mod]   [1] Invert X Axis Toggle path=NewSettings Menu/Menu/Window BG/Tabs/Gameplay Tab/Scroll/Viewport/Content/InvertXAxis/Invert X Axis Toggle
[Info   :XP Display Mod]   [2] Invert Y Axis Toggle path=NewSettings Menu/Menu/Window BG/Tabs/Gameplay Tab/Scroll/Viewport/Content/InvertYAxis/Invert Y Axis Toggle
[Info   :XP Display Mod]   [3] RunInBGToggle path=NewSettings Menu/Menu/Window BG/Tabs/Gameplay Tab/Scroll/Viewport/Content/RunInBG/RunInBGToggle
[Info   :XP Display Mod]   [4] CursorConfineToggle path=NewSettings Menu/Menu/Window BG/Tabs/Gameplay Tab/Scroll/Viewport/Content/CursorConfine/CursorConfineToggle
[Info   :XP Display Mod]   [5] VibraionEnableToggle path=NewSettings Menu/Menu/Window BG/Tabs/Gameplay Tab/Scroll/Viewport/Content/VibrationEnable/VibraionEnableToggle
[Info   :XP Display Mod]   [6] EnableShopliftersToggle path=NewSettings Menu/Menu/Window BG/Tabs/Gameplay Tab/Scroll/Viewport/Content/EnableShoplifters/EnableShopliftersToggle
[Info   :XP Display Mod]   [7] EnableOnlineOrderToggle path=NewSettings Menu/Menu/Window BG/Tabs/Gameplay Tab/Scroll/Viewport/Content/EnableOnlineOrder/EnableOnlineOrderToggle
[Info   :XP Display Mod]   [8] EnableWholeSaleToggle path=NewSettings Menu/Menu/Window BG/Tabs/Gameplay Tab/Scroll/Viewport/Content/EnableWholeSale/EnableWholeSaleToggle
[Info   :XP Display Mod]   [13] Full Screen Toggle path=NewSettings Menu/Menu/Window BG/Tabs/Graphics Tab/Scroll/Viewport/Content/FullScreen/Full Screen Toggle
[Info   :XP Display Mod]   [14] VSync Toggle path=NewSettings Menu/Menu/Window BG/Tabs/Graphics Tab/Scroll/Viewport/Content/VSync/VSync Toggle
[Info   :XP Display Mod]   [19] HDR Toggle path=NewSettings Menu/Menu/Window BG/Tabs/Graphics Tab/AdvancedGraphics/HDR/HDR Toggle
[Info   :XP Display Mod]   [20] PostProcessToggle path=NewSettings Menu/Menu/Window BG/Tabs/Graphics Tab/AdvancedGraphics/PostProcess/PostProcessToggle
[Info   :XP Display Mod] [XP] 'Language' Rect size=(876.60, 75.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(0.00, 0.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Language
[Info   :XP Display Mod] [XP]     - Language Setting
[Info   :XP Display Mod] [XP] 'MasterVolume' Rect size=(50.70, 30.00) pos=(-35.00, 0.00) anchor=(0.00, 0.50)->(0.00, 0.50) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=0.6148985,0.7723107,0.8867924,0 fillCenter=True ppu=1.5
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - MasterVolumeText
[Info   :XP Display Mod] [XP] node 'MasterVolumeSettings' NOT FOUND
[Info   :XP Display Mod] [XP] 'SFXVolume' Rect size=(50.70, 30.00) pos=(-35.00, 0.00) anchor=(0.00, 0.50)->(0.00, 0.50) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=0.6148985,0.7723107,0.8867924,0 fillCenter=True ppu=1.5
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - SFXVolumeText
[Info   :XP Display Mod] [XP] node 'SFXVolumeSettings' NOT FOUND
[Info   :XP Display Mod] [XP] 'VibrationStrength' Rect size=(876.60, 75.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(0.00, 0.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - VibrationStrengthText
[Info   :XP Display Mod] [XP]     - VibrationSetting
[Info   :XP Display Mod] [XP] node 'InterfaceButton' NOT FOUND
[Info   :XP Display Mod] [XP] 'Save Button' Rect size=(200.00, 50.00) pos=(-125.00, 73.00) anchor=(0.50, 0.00)->(0.50, 0.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.Button; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Text (TMP)
[Info   :XP Display Mod] [XP]     - GPIcon
[Info   :XP Display Mod] [XP] 'Back Button' Rect size=(200.00, 50.00) pos=(125.00, 73.00) anchor=(0.50, 0.00)->(0.50, 0.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.Button; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Text (TMP)
[Info   :XP Display Mod] [XP]     - GPIcon
[Info   :XP Display Mod] [XP] 'Item Background [0]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.9607843,0.9607843,0.9607843,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [1]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.25)->(1.00, 0.75) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=1.5
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [2]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.25)->(1.00, 0.75) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=1.5
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Invert X Axis Toggle [3]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   toggle: isOn=False targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; UIFocusScroller; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [4]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'Invert Y Axis Toggle [5]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   toggle: isOn=False targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; UIFocusScroller; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [6]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'RunInBGToggle [7]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   toggle: isOn=False targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; UIFocusScroller; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [8]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'CursorConfineToggle [9]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   toggle: isOn=True targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; UIFocusScroller; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [10]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'VibraionEnableToggle [11]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   toggle: isOn=True targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; UIFocusScroller; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [12]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'Background [13]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.25)->(1.00, 0.75) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=1.5
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'EnableShopliftersToggle [14]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   toggle: isOn=True targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; UIFocusScroller; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [15]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'EnableOnlineOrderToggle [16]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   toggle: isOn=True targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; UIFocusScroller; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [17]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'EnableWholeSaleToggle [18]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   toggle: isOn=True targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; UIFocusScroller; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [19]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=True
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'Item Background [20]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.9607843,0.9607843,0.9607843,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Item Background [21]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.9607843,0.9607843,0.9607843,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Item Background [22]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.9607843,0.9607843,0.9607843,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Item Background [23]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.9607843,0.9607843,0.9607843,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'FOV Setting Slider [24]' Rect size=(300.00, 60.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   slider: min=50 max=120 whole=True fillRect=Fill handleRect=Handle background=Background
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Slider; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; UIFocusScroller; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP]     - Fill Area
[Info   :XP Display Mod] [XP]     - Handle Slide Area
[Info   :XP Display Mod] [XP]     - Current FOV
[Info   :XP Display Mod] [XP] 'Background [25]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.25)->(1.00, 0.75) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=1.5
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Full Screen Toggle [26]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   toggle: isOn=True targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; UIFocusScroller; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [27]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'VSync Toggle [28]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   toggle: isOn=False targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; UIFocusScroller; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [29]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'Item Background [30]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.9607843,0.9607843,0.9607843,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Item Background [31]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.9607843,0.9607843,0.9607843,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Item Background [32]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.9607843,0.9607843,0.9607843,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Item Background [33]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.9607843,0.9607843,0.9607843,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'HDR Toggle [34]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   toggle: isOn=True targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [35]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'PostProcessToggle [36]' Rect size=(35.00, 35.00) pos=(-200.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   toggle: isOn=False targetGraphic=Background checkmark=Checkmark
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Toggle; __Project__.Scripts.ControllerInputModule.GamepadUISelectable; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP] 'Background [37]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=UISprite type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Checkmark
[Info   :XP Display Mod] [XP] 'Background [38]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.25)->(1.00, 0.75) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=1.5
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [39]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.25)->(1.00, 0.75) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=1.5
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [40]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.25)->(1.00, 0.75) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=0.84313726,0.93333334,1,1 fillCenter=True ppu=1.5
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'PanelParent [41]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.11)->(1.00, 0.91) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - CrosshairPanel
[Info   :XP Display Mod] [XP]     - GridPanel
[Info   :XP Display Mod] [XP]     - LayoutPanel
[Info   :XP Display Mod] [XP] 'CrosshairPanel [42]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; __Project__.Scripts.ControllerInputModule.GamePadUIPanel; __Project__.Scripts.ControllerInputModule.EventHandlers.GamepadUIRightStick; __Project__.Scripts.ControllerInputModule.EventHandlers.GamepadUINorth; __Project__.Scripts.ControllerInputModule.GamepadSelectableParent; __Project__.Scripts.PlayerAim.AimSettingsMenu; __Project__.Scripts.ControllerInputModule.EventHandlers.GamepadUITrigger; __Project__.Scripts.ControllerInputModule.EventHandlers.GamepadUIWest; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Scroll
[Info   :XP Display Mod] [XP] 'Scale Panel [43]' Rect size=(365.30, 65.50) pos=(125.00, 0.00) anchor=(0.50, 0.50)->(0.50, 0.50) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Scale
[Info   :XP Display Mod] [XP]     - Scale Slider
[Info   :XP Display Mod] [XP] 'Scale Slider [44]' Rect size=(198.09, 20.00) pos=(0.00, 0.00) anchor=(1.00, 0.50)->(1.00, 0.50) pivot=(1.00, 0.50) active=False
[Info   :XP Display Mod] [XP]   slider: min=1 max=3 whole=False fillRect=Fill handleRect=Handle background=Background
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Slider; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP]     - Fill Area
[Info   :XP Display Mod] [XP]     - Handle Slide Area
[Info   :XP Display Mod] [XP]     - GPIcon (2)
[Info   :XP Display Mod] [XP] 'Background [45]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.25)->(1.00, 0.75) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=1,1,1,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Color Panel [46]' Rect size=(300.00, 300.00) pos=(614.23, -156.00) anchor=(0.50, 1.00)->(0.50, 1.00) pivot=(0.50, 1.00) active=False
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Text Color Picker
[Info   :XP Display Mod] [XP] 'GridPanel [47]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=1,1,1,0.392 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; __Project__.Scripts.ControllerInputModule.GamePadUIPanel; __Project__.Scripts.ControllerInputModule.EventHandlers.GamepadUIRightStick; __Project__.Scripts.ControllerInputModule.EventHandlers.GamepadUINorth; __Project__.Scripts.ControllerInputModule.EventHandlers.GamepadUITrigger; __Project__.Scripts.ControllerInputModule.EventHandlers.GamepadUIWest; __Project__.Scripts.ControllerInputModule.EventHandlers.GamepadUIDPad; __Project__.Scripts.ControllerInputModule.GamepadUIFunctionLibrary; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Scroll
[Info   :XP Display Mod] [XP] 'AlphaPanel [48]' Rect size=(733.92, 65.50) pos=(-71.35, 0.00) anchor=(0.50, 0.50)->(0.50, 0.50) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - AlphaText
[Info   :XP Display Mod] [XP]     - AlphaSlider
[Info   :XP Display Mod] [XP] 'AlphaSlider [49]' Rect size=(198.09, 20.00) pos=(745.26, 0.00) anchor=(0.00, 0.50)->(0.00, 0.50) pivot=(1.00, 0.50) active=False
[Info   :XP Display Mod] [XP]   slider: min=0 max=1 whole=False fillRect=Fill handleRect=Handle background=Background
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.Slider; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP]     - Fill Area
[Info   :XP Display Mod] [XP]     - Handle Slide Area
[Info   :XP Display Mod] [XP]     - GPIcon (2)
[Info   :XP Display Mod] [XP] 'Background [50]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.25)->(1.00, 0.75) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=1,1,1,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Color Panel [51]' Rect size=(300.00, 300.00) pos=(618.00, -167.00) anchor=(0.50, 1.00)->(0.50, 1.00) pivot=(0.50, 1.00) active=False
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - GridColorPicker
[Info   :XP Display Mod] [XP] 'LayoutPanel [52]' Rect size=(0.00, -0.22) pos=(0.00, -0.11) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=Background type=Sliced color=1,1,1,0.392 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; __Project__.Scripts.ControllerInputModule.GamePadUIPanel; __Project__.Scripts.ControllerInputModule.EventHandlers.GamepadUIWest; __Project__.Scripts.ControllerInputModule.GamepadUIFunctionLibrary; __Project__.Scripts.ControllerInputModule.EventHandlers.GamepadUITrigger; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Scroll
[Info   :XP Display Mod] [XP] 'Item Background [53]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.9607843,0.9607843,0.9607843,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [54]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=S_HorizontalGradient_v1 type=Simple color=0.9803922,0.5019608,0.4470589,1 fillCenter=True ppu=8.2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [55]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [56]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [57]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [58]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [59]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [60]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [61]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [62]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [63]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [64]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [65]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [66]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [67]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=S_HorizontalGradient_v1 type=Simple color=0.9803922,0.5019608,0.4470589,1 fillCenter=True ppu=8.2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [68]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [69]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [70]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [71]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [72]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [73]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [74]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [75]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [76]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [77]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [78]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [79]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [80]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [81]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Keyboard_Player_Interaction_GridToggle [82]' Rect size=(870.49, 55.70) pos=(435.25, -1011.95) anchor=(0.00, 1.00)->(0.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.HorizontalLayoutGroup; KeybindEntryView; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP]     - ConflictBackground
[Info   :XP Display Mod] [XP]     - TitleText
[Info   :XP Display Mod] [XP]     - RebindButton
[Info   :XP Display Mod] [XP]     - ResetButton
[Info   :XP Display Mod] [XP] 'Background [83]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [84]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [85]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [86]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [87]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [88]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [89]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [90]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [91]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [92]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [93]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=S_HorizontalGradient_v1 type=Simple color=0.9803922,0.5019608,0.4470589,1 fillCenter=True ppu=8.2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [94]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [95]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [96]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [97]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [98]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [99]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [100]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [101]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [102]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [103]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [104]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=S_HorizontalGradient_v1 type=Simple color=0.9803922,0.5019608,0.4470589,1 fillCenter=True ppu=8.2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [105]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [106]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [107]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [108]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [109]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [110]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [111]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [112]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [113]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [114]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [115]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [116]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [117]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [118]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [119]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [120]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [121]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=S_HorizontalGradient_v1 type=Simple color=0.9803922,0.5019608,0.4470589,1 fillCenter=True ppu=8.2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [122]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [123]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [124]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [125]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [126]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [127]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [128]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [129]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [130]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [131]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [132]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [133]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [134]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=S_HorizontalGradient_v1 type=Simple color=0.9803922,0.5019608,0.4470589,1 fillCenter=True ppu=8.2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [135]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [136]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [137]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [138]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [139]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [140]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [141]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [142]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [143]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [144]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [145]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [146]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [147]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [148]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Keyboard_Player_Interaction_GridToggle [149]' Rect size=(0.00, 55.70) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(0.00, 0.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.UI.HorizontalLayoutGroup; KeybindEntryView; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP]     - Background
[Info   :XP Display Mod] [XP]     - ConflictBackground
[Info   :XP Display Mod] [XP]     - TitleText
[Info   :XP Display Mod] [XP]     - RebindButton
[Info   :XP Display Mod] [XP]     - ResetButton
[Info   :XP Display Mod] [XP] 'Background [150]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [151]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [152]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [153]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [154]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [155]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [156]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [157]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [158]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [159]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [160]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=S_HorizontalGradient_v1 type=Simple color=0.9803922,0.5019608,0.4470589,1 fillCenter=True ppu=8.2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [161]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [162]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [163]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [164]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [165]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [166]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [167]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [168]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [169]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [170]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [171]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=S_HorizontalGradient_v1 type=Simple color=0.9803922,0.5019608,0.4470589,1 fillCenter=True ppu=8.2
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [172]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [173]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [174]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [175]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [176]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [177]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [178]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [179]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [180]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [181]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [182]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [183]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [184]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [185]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'Background [186]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=null type=Simple color=0.19215688,0.38431376,0.6,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 
[Info   :XP Display Mod] [XP] 'ConflictBackground [187]' Rect size=(0.00, 0.00) pos=(0.00, 0.00) anchor=(0.00, 0.00)->(1.00, 1.00) pivot=(0.50, 0.50) active=False
[Info   :XP Display Mod] [XP]   image: sprite=glow_vertical_1 type=Simple color=0.8490566,0.04738707,0.0040049935,1 fillCenter=True ppu=1
[Info   :XP Display Mod] [XP]   comps: UnityEngine.RectTransform; UnityEngine.CanvasRenderer; UnityEngine.UI.Image; UnityEngine.UI.LayoutElement; 
[Info   :XP Display Mod] [XP]   children: 

## 5. Diff of ModsCfgPanel.cs in commit 9c0bf4a

```diff
commit 9c0bf4ae35ebd8ffc77a7cda2fc48a0d1063c9ce
Author: DALI951 <dali951@users.noreply.github.com>
Date:   Thu Sep 24 00:45:46 2026 +0100

    UI data-gathering pass: settings-screen dumps, native XP scan, active-instance tracking (+report)

diff --git a/ModsCfgPanel.cs b/ModsCfgPanel.cs
index ef53352..72e0aa5 100644
--- a/ModsCfgPanel.cs
+++ b/ModsCfgPanel.cs
@@ -1 +1,143 @@
-∩╗┐using System; using UnityEngine; public static class ModsCfgPanel { public static bool IsOpen; public static void Open(){ if(!IsOpen){ IsOpen=true; } } public static void Close(){ if(IsOpen){ IsOpen=false; } } public static void Toggle(){ IsOpen=!IsOpen; } public static bool Render(){ if(!IsOpen) return false; float s=Mathf.Clamp(XPDisplayMod.Scale.Value,0.5f,2f); float w=660f*s,h=620f*s; float x=(Screen.width-w)*0.5f,y=(Screen.height-h)*0.5f; GUI.color=new Color(0.02f,0.02f,0.02f,0.92f); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),XpHudRenderer.RoundedTexture()); GUI.color=new Color(0.133f,0.133f,0.133f,0.98f); GUI.DrawTexture(new Rect(x,y,w,h),XpHudRenderer.RoundedTexture()); GUI.color=new Color(1f,0.855f,0.376f,1f); GUI.DrawTexture(new Rect(x,y,w,4f*s),XpHudRenderer.RoundedTexture()); GUI.color=Color.white; return true; } }
\ No newline at end of file
+∩╗┐using System;
+using UnityEngine;
+
+public static class ModsCfgPanel
+{
+	public static bool IsOpen;
+	public static void Open() { if (!IsOpen) { IsOpen = true; } }
+	public static void Close() { if (IsOpen) { IsOpen = false; } }
+	public static void Toggle() { IsOpen = !IsOpen; }
+
+	public static bool Render()
+	{
+		if (!IsOpen) return false;
+
+		float s = Mathf.Clamp(XPDisplayMod.Scale.Value, 0.5f, 2f);
+		float w = 660f * s, h = 620f * s;
+		float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;
+
+		GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.92f);
+		GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), XpHudRenderer.RoundedTexture());
+		GUI.color = new Color(0.133f, 0.133f, 0.133f, 0.98f);
+		GUI.DrawTexture(new Rect(x, y, w, h), XpHudRenderer.RoundedTexture());
+
+		GUI.color = new Color(1f, 0.855f, 0.376f, 1f);
+		GUI.DrawTexture(new Rect(x, y, w, 4f * s), XpHudRenderer.RoundedTexture());
+		GUI.color = Color.white;
+
+		EnsureCfgStyles();
+
+		float pad = 24f * s;
+		float rowH = 30f * s;
+		float cx = x + pad;
+		float cy = y + 20f * s;
+		float cw = w - pad * 2f;
+
+		GUI.color = new Color(1f, 0.855f, 0.376f, 1f);
+		GUI.Label(new Rect(cx, cy, cw, 28f * s), "XP DISPLAY SETTINGS", _cfgTitle);
+		GUI.color = Color.white;
+		cy += 36f * s;
+
+		XPDisplayMod.ShowBar.Value        = ToggleRow(cx, ref cy, cw, rowH, "Show XP Bar", XPDisplayMod.ShowBar.Value);
+		XPDisplayMod.ShowPopup.Value      = ToggleRow(cx, ref cy, cw, rowH, "Show Level-Up Popup", XPDisplayMod.ShowPopup.Value);
+		XPDisplayMod.ShowFloaters.Value   = ToggleRow(cx, ref cy, cw, rowH, "Show Floaters", XPDisplayMod.ShowFloaters.Value);
+		XPDisplayMod.ShowStats.Value      = ToggleRow(cx, ref cy, cw, rowH, "Show Session Stats", XPDisplayMod.ShowStats.Value);
+		XPDisplayMod.RewardEnabled.Value  = ToggleRow(cx, ref cy, cw, rowH, "Reward Cash on Level-Up", XPDisplayMod.RewardEnabled.Value);
+
+		cy += 8f * s;
+		XPDisplayMod.Scale.Value          = SliderRow(cx, ref cy, cw, rowH, "UI Scale", XPDisplayMod.Scale.Value, 0.5f, 2f);
+		XPDisplayMod.RewardPerLevel.Value = SliderRow(cx, ref cy, cw, rowH, "Reward Per Level ($)", XPDisplayMod.RewardPerLevel.Value, 0f, 500f);
+
+		cy += 8f * s;
+		ColorRow(cx, ref cy, cw, rowH, "Background Color", XPDisplayMod.BgColor);
+		ColorRow(cx, ref cy, cw, rowH, "Accent Color", XPDisplayMod.AccentColor);
+		ColorRow(cx, ref cy, cw, rowH, "Text Color", XPDisplayMod.TextColor);
+
+		float btnW = 100f * s, btnH = 28f * s;
+		Rect closeRect = new Rect(x + w - btnW - pad, y + h - btnH - 16f * s, btnW, btnH);
+		GUI.color = new Color(1f, 0.855f, 0.376f, 1f);
+		GUI.DrawTexture(closeRect, XpHudRenderer.RoundedTexture());
+		GUI.color = Color.white;
+		if (GUI.Button(closeRect, "Close", _cfgClose))
+		{
+			Close();
+		}
+
+		GUI.color = Color.white;
+		return true;
+	}
+
+	private static GUIStyle _cfgTitle;
+	private static GUIStyle _cfgLabel;
+	private static GUIStyle _cfgClose;
+
+	private static void EnsureCfgStyles()
+	{
+		if (_cfgTitle != null) return;
+		GUIStyle baseStyle = GUI.skin.label;
+
+		_cfgTitle = new GUIStyle();
+		_cfgTitle.font = baseStyle.font;
+		_cfgTitle.fontStyle = FontStyle.Bold;
+		_cfgTitle.fontSize = 20;
+		_cfgTitle.alignment = TextAnchor.MiddleLeft;
+		_cfgTitle.normal.textColor = Color.white;
+
+		_cfgLabel = new GUIStyle();
+		_cfgLabel.font = baseStyle.font;
+		_cfgLabel.fontStyle = FontStyle.Normal;
+		_cfgLabel.fontSize = 14;
+		_cfgLabel.alignment = TextAnchor.MiddleLeft;
+		_cfgLabel.normal.textColor = Color.white;
+
+		_cfgClose = new GUIStyle();
+		_cfgClose.font = GUI.skin.button.font;
+		_cfgClose.fontSize = GUI.skin.button.fontSize;
+		_cfgClose.normal = GUI.skin.button.normal;
+		_cfgClose.hover = GUI.skin.button.hover;
+		_cfgClose.active = GUI.skin.button.active;
+		_cfgClose.border = GUI.skin.button.border;
+		_cfgClose.padding = GUI.skin.button.padding;
+		_cfgClose.fontStyle = FontStyle.Bold;
+		_cfgClose.alignment = TextAnchor.MiddleCenter;
+	}
+
+	private static bool ToggleRow(float x, ref float y, float w, float rowH, string label, bool value)
+	{
+		GUI.color = Color.white;
+		bool result = GUI.Toggle(new Rect(x, y, w, rowH), value, " " + label, _cfgLabel);
+		y += rowH;
+		return result;
+	}
+
+	private static float SliderRow(float x, ref float y, float w, float rowH, string label, float value, float min, float max)
+	{
+		GUI.color = Color.white;
+		GUI.Label(new Rect(x, y, w * 0.4f, rowH), label + ": " + value.ToString("0.0"), _cfgLabel);
+		float result = GUI.HorizontalSlider(new Rect(x + w * 0.42f, y + rowH * 0.4f, w * 0.58f, rowH * 0.2f), value, min, max);
+		y += rowH;
+		return result;
+	}
+
+	private static void ColorRow(float x, ref float y, float w, float rowH, string label, BepInEx.Configuration.ConfigEntry<string> entry)
+	{
+		Color c = XPDisplayMod.ParseColor(entry.Value, Color.white);
+		GUI.color = Color.white;
+		GUI.Label(new Rect(x, y, w * 0.3f, rowH), label, _cfgLabel);
+
+		float r = GUI.HorizontalSlider(new Rect(x + w * 0.32f, y + rowH * 0.55f, w * 0.2f, rowH * 0.2f), c.r, 0f, 1f);
+		float g = GUI.HorizontalSlider(new Rect(x + w * 0.54f, y + rowH * 0.55f, w * 0.2f, rowH * 0.2f), c.g, 0f, 1f);
+		float b = GUI.HorizontalSlider(new Rect(x + w * 0.76f, y + rowH * 0.55f, w * 0.2f, rowH * 0.2f), c.b, 0f, 1f);
+
+		GUI.color = new Color(r, g, b, c.a);
+		GUI.DrawTexture(new Rect(x + w * 0.32f, y, w * 0.64f, rowH * 0.4f), XpHudRenderer.RoundedTexture());
+		GUI.color = Color.white;
+
+		entry.Value = r.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
+					  g.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
+					  b.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
+					  c.a.ToString(System.Globalization.CultureInfo.InvariantCulture);
+
+		y += rowH;
+	}
+}
\ No newline at end of file
```

## 6. Diff of XPDisplayBehaviour.cs in commit 9c0bf4a

```diff
commit 9c0bf4ae35ebd8ffc77a7cda2fc48a0d1063c9ce
Author: DALI951 <dali951@users.noreply.github.com>
Date:   Thu Sep 24 00:45:46 2026 +0100

    UI data-gathering pass: settings-screen dumps, native XP scan, active-instance tracking (+report)

diff --git a/XPDisplayBehaviour.cs b/XPDisplayBehaviour.cs
index e816312..b96904e 100644
--- a/XPDisplayBehaviour.cs
+++ b/XPDisplayBehaviour.cs
@@ -1,5 +1,6 @@
 ∩╗┐using System;
 using System.Collections.Generic;
+using Il2CppInterop.Runtime;
 using Il2CppInterop.Runtime.InteropTypes.Arrays;
 using UnityEngine;
 
@@ -16,6 +17,7 @@ public class XPDisplayBehaviour : MonoBehaviour
 	private SettingsMenuManager _settingsMenu;
 	private float _scanTimer;
 	private bool _settingsDumpDone;
+	private bool _nativeXpScanDone;
 
 	// state (kept in sync by the game's own events - zero polling)
 	private int _currentXp;
@@ -108,14 +110,42 @@ public class XPDisplayBehaviour : MonoBehaviour
 	{
 		try
 		{
-			if (_settingsMenu == null)
+			if (_settingsMenu == null || !_settingsMenu.gameObject.activeInHierarchy)
 			{
 				SettingsMenuManager[] found = UnityEngine.Object.FindObjectsOfType<SettingsMenuManager>();
 				if (found != null && found.Length > 0)
 				{
-					_settingsMenu = found[0];
+					// track the settings screen that is actually ACTIVE (main menu vs in-game
+					// Escape Menu spawn separate instances - follow the visible one)
+					SettingsMenuManager activeInstance = null;
+					SettingsMenuManager activeRootInstance = null;
+					for (int i = 0; i < found.Length; i++)
+					{
+						Transform m = found[i] != null ? FindChild(found[i].transform, "Menu") : null;
+						if (found[i].gameObject.activeInHierarchy && m != null && m.gameObject.activeInHierarchy)
+						{
+							activeInstance = found[i];
+							break;
+						}
+						if (activeRootInstance == null && found[i] != null && found[i].gameObject.activeInHierarchy)
+						{
+							activeRootInstance = found[i];
+						}
+					}
+					if (activeInstance != null)
+					{
+						_settingsMenu = activeInstance;
+					}
+					else if (activeRootInstance != null)
+					{
+						_settingsMenu = activeRootInstance;
+					}
+					else if (_settingsMenu == null)
+					{
+						_settingsMenu = found[0];
+					}
 					var log = XPDisplayMod.Instance.Log;
-					log.LogInfo("[XP] SettingsMenuManager instances: " + found.Length);
+					log.LogInfo("[XP] SettingsMenuManager instances: " + found.Length + " tracking=" + _settingsMenu.gameObject.name);
 					for (int i = 0; i < found.Length; i++)
 					{
 						log.LogInfo("[XP]   [" + i + "] root=" + found[i].gameObject.name +
@@ -150,14 +180,18 @@ public class XPDisplayBehaviour : MonoBehaviour
 				}
 			}
 
-			// one-shot dump + live injection happen while the settings panel is OPEN
+			// one-shot dump + live injection happen while the settings panel is OPEN.
+			// in-game the visible settings screen is XboxSettingMenu (no "Menu" child),
+			// so fall back to the tracked instance root being active in the hierarchy.
 			Transform menu = _settingsMenu != null ? FindChild(_settingsMenu.transform, "Menu") : null;
-			bool openNow = menu != null && menu.gameObject.activeInHierarchy;
+			bool openNow = _settingsMenu != null && (menu != null
+				? menu.gameObject.activeInHierarchy
+				: _settingsMenu.gameObject.activeInHierarchy);
 			if (openNow && !_settingsDumpDone)
 			{
 				DumpSettingsScreen();
 			}
-			if (openNow)
+			if (openNow && menu != null)
 			{
 				TryInjectSettingsButton(menu);
 				EnforceStrip();
@@ -217,7 +251,7 @@ public class XPDisplayBehaviour : MonoBehaviour
 			{
 				UnityEngine.Object.Destroy(clone.transform.GetChild(i).gameObject);
 			}
-			var labelGo = new GameObject("XPDisplayLabel");
+			var labelGo = new GameObject("XPDisplayLabel", Il2CppType.Of<RectTransform>(), Il2CppType.Of<UnityEngine.UI.Text>());
 			labelGo.transform.SetParent(clone.transform, false);
 			RectTransform lrt = labelGo.GetComponent<RectTransform>();
 			lrt.anchorMin = Vector2.zero;
@@ -242,7 +276,7 @@ public class XPDisplayBehaviour : MonoBehaviour
 			// native tab text has a subtle 1px drop shadow - replicate with UGUI Shadow
 			try
 			{
-				UnityEngine.UI.Shadow sh = 
+				UnityEngine.UI.Shadow sh = clone.AddComponent(Il2CppType.Of<UnityEngine.UI.Shadow>()).Cast<UnityEngine.UI.Shadow>();
 				sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
 				sh.effectDistance = new Vector2(1f, -1f);
 			}
@@ -253,7 +287,7 @@ public class XPDisplayBehaviour : MonoBehaviour
 			UnityEngine.UI.Button btn = clone.GetComponent<UnityEngine.UI.Button>();
 			if (btn == null)
 			{
-				btn = 
+				btn = clone.AddComponent(Il2CppType.Of<UnityEngine.UI.Button>()).Cast<UnityEngine.UI.Button>();
 			}
 			btn.onClick.RemoveAllListeners();
 			btn.onClick.AddListener((UnityEngine.Events.UnityAction)OnModsButtonClicked);
@@ -383,15 +417,40 @@ public class XPDisplayBehaviour : MonoBehaviour
 			log.LogInfo("=== [XP] Settings screen tree ===");
 			DumpTransform(_settingsMenu.transform, 0, log);
 			UnityEngine.UI.Button[] btns = _settingsMenu.GetComponentsInChildren<UnityEngine.UI.Button>(true);
-			log.LogInfo("=== [XP] Buttons: " + btns.Length);
+log.LogInfo("=== [XP] Buttons: " + btns.Length);
 			for (int i = 0; i < btns.Length; i++)
 			{
 				UnityEngine.UI.Button b = btns[i];
 				string text = GetChildText(b.transform);
 				log.LogInfo("  [" + i + "] " + b.gameObject.name + "  text='" + text + "'  parent=" + b.transform.parent.name);
 			}
+			try
+			{
+				UnityEngine.UI.Slider[] sliders = _settingsMenu.GetComponentsInChildren<UnityEngine.UI.Slider>(true);
+				log.LogInfo("=== [XP] Sliders: " + sliders.Length);
+				for (int i = 0; i < sliders.Length; i++)
+				{
+					log.LogInfo("  [" + i + "] " + sliders[i].gameObject.name + " path=" + FindPath(sliders[i].transform));
+				}
+			}
+			catch (Exception ex)
+			{
+				log.LogInfo("[XP] slider list failed: " + ex.Message);
+			}
+			try
+			{
+				UnityEngine.UI.Toggle[] toggles = _settingsMenu.GetComponentsInChildren<UnityEngine.UI.Toggle>(true);
+				log.LogInfo("=== [XP] Toggles: " + toggles.Length);
+				for (int i = 0; i < toggles.Length; i++)
+				{
+					log.LogInfo("  [" + i + "] " + toggles[i].gameObject.name + " path=" + FindPath(toggles[i].transform));
+				}
+			}
+			catch (Exception ex)
+			{
+				log.LogInfo("[XP] toggle list failed: " + ex.Message);
+			}
 			log.LogInfo("=== [XP] Node inspector ===");
-			DumpNode(_settingsMenu.transform, "Menu");
 			DumpNode(_settingsMenu.transform, "Language");
 			DumpNode(_settingsMenu.transform, "MasterVolume");
 			DumpNode(_settingsMenu.transform, "MasterVolumeSettings");
@@ -401,6 +460,31 @@ public class XPDisplayBehaviour : MonoBehaviour
 			DumpNode(_settingsMenu.transform, "InterfaceButton");
 			DumpNode(_settingsMenu.transform, "Save Button");
 			DumpNode(_settingsMenu.transform, "Back Button");
+			try
+			{
+				RectTransform[] namedRects = _settingsMenu.GetComponentsInChildren<RectTransform>(true);
+				int matchCount = 0;
+				for (int i = 0; i < namedRects.Length; i++)
+				{
+					string n2 = namedRects[i].gameObject.name;
+					if (n2 == null) continue;
+					bool hit = n2.IndexOf("Toggle", StringComparison.OrdinalIgnoreCase) >= 0 ||
+						n2.IndexOf("Slider", StringComparison.OrdinalIgnoreCase) >= 0 ||
+						n2.IndexOf("Checkbox", StringComparison.OrdinalIgnoreCase) >= 0 ||
+						n2.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0 ||
+						n2.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0;
+					if (hit)
+					{
+						DumpNodeTransform(namedRects[i], n2 + " [" + matchCount + "]");
+						matchCount++;
+					}
+				}
+				log.LogInfo("=== [XP] Matched-name node dumps: " + matchCount);
+			}
+			catch (Exception ex)
+			{
+				log.LogInfo("[XP] matched-name scan failed: " + ex.Message);
+			}
 		}
 		catch (Exception ex)
 		{
@@ -489,6 +573,36 @@ public class XPDisplayBehaviour : MonoBehaviour
 		return t.parent != null ? FindPath(t.parent) + "/" + t.gameObject.name : t.gameObject.name;
 	}
 
+	// one-shot: native XP/level/progress/store HUD component scan (data gathering)
+	private void ScanNativeXpNodes()
+	{
+		if (_nativeXpScanDone) return;
+		_nativeXpScanDone = true;
+		try
+		{
+			RectTransform[] all = UnityEngine.Object.FindObjectsOfType<RectTransform>(true);
+			XPDisplayMod.Instance.Log.LogInfo("[XP] native-XP scan: " + all.Length + " rects");
+			for (int i = 0; i < all.Length; i++)
+			{
+				string n = all[i].gameObject.name;
+				if (n != null && (n.IndexOf("xp", StringComparison.OrdinalIgnoreCase) >= 0 ||
+					n.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0 ||
+					n.IndexOf("exp", StringComparison.OrdinalIgnoreCase) >= 0 ||
+					n.IndexOf("progress", StringComparison.OrdinalIgnoreCase) >= 0 ||
+					n.IndexOf("store", StringComparison.OrdinalIgnoreCase) >= 0))
+				{
+					XPDisplayMod.Instance.Log.LogInfo("[XP] native-XP candidate: '" + n + "' parent=" +
+						(all[i].parent != null ? all[i].parent.gameObject.name : "none") + " path=" + FindPath(all[i]));
+				}
+			}
+			XPDisplayMod.Instance.Log.LogInfo("[XP] native-XP scan done");
+		}
+		catch (Exception ex)
+		{
+			XPDisplayMod.Instance.Log.LogError("[XP] native-XP scan failed: " + ex.Message);
+		}
+	}
+
 	private static Transform FindChild(Transform root, string name)
 	{
 		if (root == null) return null;
@@ -509,16 +623,21 @@ public class XPDisplayBehaviour : MonoBehaviour
 			XPDisplayMod.Instance.Log.LogInfo("[XP] node '" + name + "' NOT FOUND");
 			return;
 		}
+		DumpNodeTransform(t, name);
+	}
+
+	private static void DumpNodeTransform(Transform t, string label)
+	{
 		var log = XPDisplayMod.Instance.Log;
 		RectTransform rect = t.GetComponent<RectTransform>();
 		if (rect != null)
 		{
-			log.LogInfo("[XP] '" + name + "' Rect size=" + rect.sizeDelta + " pos=" + rect.anchoredPosition +
+			log.LogInfo("[XP] '" + label + "' Rect size=" + rect.sizeDelta + " pos=" + rect.anchoredPosition +
 				" anchor=" + rect.anchorMin + "->" + rect.anchorMax + " pivot=" + rect.pivot + " active=" + t.gameObject.activeInHierarchy);
 		}
 		else
 		{
-			log.LogInfo("[XP] '" + name + "' Transform localPos=" + t.localPosition + " active=" + t.gameObject.activeInHierarchy);
+			log.LogInfo("[XP] '" + label + "' Transform localPos=" + t.localPosition + " active=" + t.gameObject.activeInHierarchy);
 		}
 		UnityEngine.UI.Text txtComp = t.GetComponent<UnityEngine.UI.Text>();
 		if (txtComp != null)
@@ -526,18 +645,112 @@ public class XPDisplayBehaviour : MonoBehaviour
 			log.LogInfo("[XP]   text: '" + txtComp.text + "' font=" + txtComp.font.name + " size=" + txtComp.fontSize +
 				" color=" + txtComp.color + " align=" + txtComp.alignment + " bold=" + txtComp.fontStyle);
 		}
-		Component[] comps = t.GetComponents<Component>();
-		string list = "";
-		for (int i = 0; i < comps.Length; i++)
+
+		// component detail pass (data gathering for native-UI cloning)
+		try
 		{
-			try
+			UnityEngine.UI.Image img = t.GetComponent<UnityEngine.UI.Image>();
+			if (img != null)
 			{
-				list += comps[i].GetIl2CppType().FullName + "; ";
+				string spriteName = "null";
+				try { if (img.sprite != null) spriteName = img.sprite.name; } catch (Exception) { }
+				string typeStr = "?";
+				try
+				{
+					if (img.type == UnityEngine.UI.Image.Type.Simple) typeStr = "Simple";
+					else if (img.type == UnityEngine.UI.Image.Type.Sliced) typeStr = "Sliced";
+					else if (img.type == UnityEngine.UI.Image.Type.Tiled) typeStr = "Tiled";
+					else if (img.type == UnityEngine.UI.Image.Type.Filled) typeStr = "Filled";
+				}
+				catch (Exception) { }
+				log.LogInfo("[XP]   image: sprite=" + spriteName + " type=" + typeStr +
+					" color=" + img.color.r + "," + img.color.g + "," + img.color.b + "," + img.color.a +
+					" fillCenter=" + img.fillCenter + " ppu=" + img.pixelsPerUnitMultiplier);
 			}
-			catch (Exception)
+		}
+		catch (Exception) { }
+
+		try
+		{
+			TMPro.TextMeshProUGUI tmp = t.GetComponent<TMPro.TextMeshProUGUI>();
+			if (tmp != null)
 			{
+				string fontName = "null";
+				try { if (tmp.font != null) fontName = tmp.font.name; } catch (Exception) { }
+				string fstyle = "?";
+				try
+				{
+					if (tmp.fontStyle == TMPro.FontStyles.Normal) fstyle = "Normal";
+					else fstyle = tmp.fontStyle.ToString();
+				}
+				catch (Exception)
+				{
+					fstyle = "?";
+				}
+				log.LogInfo("[XP]   tmp: font=" + fontName + " size=" + tmp.fontSize +
+					" color=" + tmp.color.r + "," + tmp.color.g + "," + tmp.color.b + "," + tmp.color.a +
+					" style=" + fstyle);
+			}
+		}
+		catch (Exception) { }
+
+		try
+		{
+			UnityEngine.UI.Toggle tg = t.GetComponent<UnityEngine.UI.Toggle>();
+			if (tg != null)
+			{
+				string targetName = "null", graphicName = "null";
+				try { if (tg.targetGraphic != null) targetName = tg.targetGraphic.gameObject.name; } catch (Exception) { }
+				try { if (tg.graphic != null) graphicName = tg.graphic.gameObject.name; } catch (Exception) { }
+				log.LogInfo("[XP]   toggle: isOn=" + tg.isOn + " targetGraphic=" + targetName + " checkmark=" + graphicName);
 			}
 		}
+		catch (Exception) { }
+
+		try
+		{
+			UnityEngine.UI.Slider sl = t.GetComponent<UnityEngine.UI.Slider>();
+			if (sl != null)
+			{
+				string fillName = "null", handleName = "null", backgroundName = "none";
+				try { if (sl.fillRect != null) fillName = sl.fillRect.name; } catch (Exception) { }
+				try { if (sl.handleRect != null) handleName = sl.handleRect.name; } catch (Exception) { }
+				try
+				{
+					for (int c = 0; c < t.childCount; c++)
+					{
+						if (t.GetChild(c).gameObject.name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0)
+						{
+							backgroundName = t.GetChild(c).gameObject.name;
+							break;
+						}
+					}
+				}
+				catch (Exception) { }
+				log.LogInfo("[XP]   slider: min=" + sl.minValue + " max=" + sl.maxValue + " whole=" + sl.wholeNumbers +
+					" fillRect=" + fillName + " handleRect=" + handleName + " background=" + backgroundName);
+			}
+		}
+		catch (Exception) { }
+
+		try
+		{
+			Component[] comps = t.GetComponents<Component>();
+			string list = "";
+			for (int i = 0; i < comps.Length; i++)
+			{
+				try
+				{
+					list += comps[i].GetIl2CppType().FullName + "; ";
+				}
+				catch (Exception)
+				{
+				}
+			}
+			log.LogInfo("[XP]   comps: " + list);
+		}
+		catch (Exception) { }
+
 		log.LogInfo("[XP]   children: ");
 		for (int i = 0; i < t.childCount; i++)
 		{
@@ -589,6 +802,7 @@ public class XPDisplayBehaviour : MonoBehaviour
 
 			SyncLevelState();
 			Subscribe();
+			ScanNativeXpNodes();
 		}
 		catch (Exception ex)
 		{
```

## 7. Diff of XPDisplayMod.csproj in commit 9c0bf4a

```diff
commit 9c0bf4ae35ebd8ffc77a7cda2fc48a0d1063c9ce
Author: DALI951 <dali951@users.noreply.github.com>
Date:   Thu Sep 24 00:45:46 2026 +0100

    UI data-gathering pass: settings-screen dumps, native XP scan, active-instance tracking (+report)

diff --git a/XPDisplayMod.csproj b/XPDisplayMod.csproj
index 46fbbcc..3c8ee6d 100644
--- a/XPDisplayMod.csproj
+++ b/XPDisplayMod.csproj
@@ -78,6 +78,9 @@
     <Reference Include="UnityEngine.UI">
       <HintPath>C:\SteamUnlocked\Supermarket.Sim.v1.6.0.ALL.DLC\Supermarket.Sim.v1.6.0.ALL.DLC\BepInEx\interop\UnityEngine.UI.dll</HintPath>
     </Reference>
+    <Reference Include="Unity.TextMeshPro">
+      <HintPath>C:\SteamUnlocked\Supermarket.Sim.v1.6.0.ALL.DLC\Supermarket.Sim.v1.6.0.ALL.DLC\BepInEx\interop\Unity.TextMeshPro.dll</HintPath>
+    </Reference>
   </ItemGroup>
   <ItemGroup>
     <AppDesigner Include="Properties\" />
```

## 8. Current full contents of ModsCfgPanel.cs

```csharp
using System;
using UnityEngine;

public static class ModsCfgPanel
{
	public static bool IsOpen;
	public static void Open() { if (!IsOpen) { IsOpen = true; } }
	public static void Close() { if (IsOpen) { IsOpen = false; } }
	public static void Toggle() { IsOpen = !IsOpen; }

	public static bool Render()
	{
		if (!IsOpen) return false;

		float s = Mathf.Clamp(XPDisplayMod.Scale.Value, 0.5f, 2f);
		float w = 660f * s, h = 620f * s;
		float x = (Screen.width - w) * 0.5f, y = (Screen.height - h) * 0.5f;

		GUI.color = new Color(0.02f, 0.02f, 0.02f, 0.92f);
		GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), XpHudRenderer.RoundedTexture());
		GUI.color = new Color(0.133f, 0.133f, 0.133f, 0.98f);
		GUI.DrawTexture(new Rect(x, y, w, h), XpHudRenderer.RoundedTexture());

		GUI.color = new Color(1f, 0.855f, 0.376f, 1f);
		GUI.DrawTexture(new Rect(x, y, w, 4f * s), XpHudRenderer.RoundedTexture());
		GUI.color = Color.white;

		EnsureCfgStyles();

		float pad = 24f * s;
		float rowH = 30f * s;
		float cx = x + pad;
		float cy = y + 20f * s;
		float cw = w - pad * 2f;

		GUI.color = new Color(1f, 0.855f, 0.376f, 1f);
		GUI.Label(new Rect(cx, cy, cw, 28f * s), "XP DISPLAY SETTINGS", _cfgTitle);
		GUI.color = Color.white;
		cy += 36f * s;

		XPDisplayMod.ShowBar.Value        = ToggleRow(cx, ref cy, cw, rowH, "Show XP Bar", XPDisplayMod.ShowBar.Value);
		XPDisplayMod.ShowPopup.Value      = ToggleRow(cx, ref cy, cw, rowH, "Show Level-Up Popup", XPDisplayMod.ShowPopup.Value);
		XPDisplayMod.ShowFloaters.Value   = ToggleRow(cx, ref cy, cw, rowH, "Show Floaters", XPDisplayMod.ShowFloaters.Value);
		XPDisplayMod.ShowStats.Value      = ToggleRow(cx, ref cy, cw, rowH, "Show Session Stats", XPDisplayMod.ShowStats.Value);
		XPDisplayMod.RewardEnabled.Value  = ToggleRow(cx, ref cy, cw, rowH, "Reward Cash on Level-Up", XPDisplayMod.RewardEnabled.Value);

		cy += 8f * s;
		XPDisplayMod.Scale.Value          = SliderRow(cx, ref cy, cw, rowH, "UI Scale", XPDisplayMod.Scale.Value, 0.5f, 2f);
		XPDisplayMod.RewardPerLevel.Value = SliderRow(cx, ref cy, cw, rowH, "Reward Per Level ($)", XPDisplayMod.RewardPerLevel.Value, 0f, 500f);

		cy += 8f * s;
		ColorRow(cx, ref cy, cw, rowH, "Background Color", XPDisplayMod.BgColor);
		ColorRow(cx, ref cy, cw, rowH, "Accent Color", XPDisplayMod.AccentColor);
		ColorRow(cx, ref cy, cw, rowH, "Text Color", XPDisplayMod.TextColor);

		float btnW = 100f * s, btnH = 28f * s;
		Rect closeRect = new Rect(x + w - btnW - pad, y + h - btnH - 16f * s, btnW, btnH);
		GUI.color = new Color(1f, 0.855f, 0.376f, 1f);
		GUI.DrawTexture(closeRect, XpHudRenderer.RoundedTexture());
		GUI.color = Color.white;
		if (GUI.Button(closeRect, "Close", _cfgClose))
		{
			Close();
		}

		GUI.color = Color.white;
		return true;
	}

	private static GUIStyle _cfgTitle;
	private static GUIStyle _cfgLabel;
	private static GUIStyle _cfgClose;

	private static void EnsureCfgStyles()
	{
		if (_cfgTitle != null) return;
		GUIStyle baseStyle = GUI.skin.label;

		_cfgTitle = new GUIStyle();
		_cfgTitle.font = baseStyle.font;
		_cfgTitle.fontStyle = FontStyle.Bold;
		_cfgTitle.fontSize = 20;
		_cfgTitle.alignment = TextAnchor.MiddleLeft;
		_cfgTitle.normal.textColor = Color.white;

		_cfgLabel = new GUIStyle();
		_cfgLabel.font = baseStyle.font;
		_cfgLabel.fontStyle = FontStyle.Normal;
		_cfgLabel.fontSize = 14;
		_cfgLabel.alignment = TextAnchor.MiddleLeft;
		_cfgLabel.normal.textColor = Color.white;

		_cfgClose = new GUIStyle();
		_cfgClose.font = GUI.skin.button.font;
		_cfgClose.fontSize = GUI.skin.button.fontSize;
		_cfgClose.normal = GUI.skin.button.normal;
		_cfgClose.hover = GUI.skin.button.hover;
		_cfgClose.active = GUI.skin.button.active;
		_cfgClose.border = GUI.skin.button.border;
		_cfgClose.padding = GUI.skin.button.padding;
		_cfgClose.fontStyle = FontStyle.Bold;
		_cfgClose.alignment = TextAnchor.MiddleCenter;
	}

	private static bool ToggleRow(float x, ref float y, float w, float rowH, string label, bool value)
	{
		GUI.color = Color.white;
		bool result = GUI.Toggle(new Rect(x, y, w, rowH), value, " " + label, _cfgLabel);
		y += rowH;
		return result;
	}

	private static float SliderRow(float x, ref float y, float w, float rowH, string label, float value, float min, float max)
	{
		GUI.color = Color.white;
		GUI.Label(new Rect(x, y, w * 0.4f, rowH), label + ": " + value.ToString("0.0"), _cfgLabel);
		float result = GUI.HorizontalSlider(new Rect(x + w * 0.42f, y + rowH * 0.4f, w * 0.58f, rowH * 0.2f), value, min, max);
		y += rowH;
		return result;
	}

	private static void ColorRow(float x, ref float y, float w, float rowH, string label, BepInEx.Configuration.ConfigEntry<string> entry)
	{
		Color c = XPDisplayMod.ParseColor(entry.Value, Color.white);
		GUI.color = Color.white;
		GUI.Label(new Rect(x, y, w * 0.3f, rowH), label, _cfgLabel);

		float r = GUI.HorizontalSlider(new Rect(x + w * 0.32f, y + rowH * 0.55f, w * 0.2f, rowH * 0.2f), c.r, 0f, 1f);
		float g = GUI.HorizontalSlider(new Rect(x + w * 0.54f, y + rowH * 0.55f, w * 0.2f, rowH * 0.2f), c.g, 0f, 1f);
		float b = GUI.HorizontalSlider(new Rect(x + w * 0.76f, y + rowH * 0.55f, w * 0.2f, rowH * 0.2f), c.b, 0f, 1f);

		GUI.color = new Color(r, g, b, c.a);
		GUI.DrawTexture(new Rect(x + w * 0.32f, y, w * 0.64f, rowH * 0.4f), XpHudRenderer.RoundedTexture());
		GUI.color = Color.white;

		entry.Value = r.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
					  g.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
					  b.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
					  c.a.ToString(System.Globalization.CultureInfo.InvariantCulture);

		y += rowH;
	}
}
```

## 9. Current full contents of XPDisplayBehaviour.cs

```csharp
using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

// Token: 0x02000003 RID: 3
public class XPDisplayBehaviour : MonoBehaviour
{
	private StoreLevelManager _manager;
	private bool _subscribed;
	private bool _visible = true;
	private KeyCode _toggle;
	private KeyCode _cfgKey;

	// settings-screen detection (docks the "MODS" button next to the game's own settings)
	private SettingsMenuManager _settingsMenu;
	private float _scanTimer;
	private bool _settingsDumpDone;
	private bool _nativeXpScanDone;

	// state (kept in sync by the game's own events - zero polling)
	private int _currentXp;
	private int _requiredXp;
	private int _level;
	private int _maxLevels;
	private bool _isLast;
	private int _prevRequirement;
	private bool _branchCumulative;
	private float _fillTarget;
	private float _fillShown;

	// popup
	private float _popupStart = -999f;
	private string _popupTitle = "";
	private string _popupSub = "";

	// floaters
	public class Floater
	{
		public string Text;
		public Color Color;
		public float Start;
	}

	private readonly List<Floater> _floaters = new List<Floater>();
	private const float FloaterLifetime = 1.2f;
	private const int MaxFloaters = 6;

	// stats
	private int _sessionXp;
	private int _levelUps;
	private float _sessionStart;
	private float _lastWindowSecond;

	private void Start()
	{
		_sessionStart = Time.unscaledTime;
		_lastWindowSecond = _sessionStart;
		_toggle = ParseKey(XPDisplayMod.ToggleKey.Value, KeyCode.F9);
		_cfgKey = ParseKey(XPDisplayMod.CfgKey.Value, KeyCode.F2);
	}

	private void Update()
	{
		// settings-button injection MUST run in menus too - before the store-level gate
		_scanTimer -= Time.unscaledDeltaTime;
		if (_scanTimer <= 0f)
		{
			_scanTimer = 1f;
			ScanForSettingsScreen();
		}

		if (_manager == null)
		{
			// dashboard/menu phase - wait until the game world exists
			if (NoktaSingleton<StoreLevelManager>.HasInstance)
			{
				_manager = NoktaSingleton<StoreLevelManager>.Instance;
				OnManagerAcquired();
			}
			return;
		}

		// smooth bar animation (only when the target actually changed)
		float target = _fillTarget;
		if (Mathf.Abs(_fillShown - target) > 0.0005f)
		{
			_fillShown = Mathf.MoveTowards(_fillShown, target, Time.deltaTime * 1.5f);
		}

		if (Input.GetKeyDown(_toggle))
		{
			_visible = !_visible;
			if (!_visible)
			{
				_floaters.Clear();
			}
		}

		if (Input.GetKeyDown(_cfgKey))
		{
			ModsCfgPanel.IsOpen = !ModsCfgPanel.IsOpen;
		}
	}

	private bool _scanLogged;

	private void ScanForSettingsScreen()
	{
		try
		{
			if (_settingsMenu == null || !_settingsMenu.gameObject.activeInHierarchy)
			{
				SettingsMenuManager[] found = UnityEngine.Object.FindObjectsOfType<SettingsMenuManager>();
				if (found != null && found.Length > 0)
				{
					// track the settings screen that is actually ACTIVE (main menu vs in-game
					// Escape Menu spawn separate instances - follow the visible one)
					SettingsMenuManager activeInstance = null;
					SettingsMenuManager activeRootInstance = null;
					for (int i = 0; i < found.Length; i++)
					{
						Transform m = found[i] != null ? FindChild(found[i].transform, "Menu") : null;
						if (found[i].gameObject.activeInHierarchy && m != null && m.gameObject.activeInHierarchy)
						{
							activeInstance = found[i];
							break;
						}
						if (activeRootInstance == null && found[i] != null && found[i].gameObject.activeInHierarchy)
						{
							activeRootInstance = found[i];
						}
					}
					if (activeInstance != null)
					{
						_settingsMenu = activeInstance;
					}
					else if (activeRootInstance != null)
					{
						_settingsMenu = activeRootInstance;
					}
					else if (_settingsMenu == null)
					{
						_settingsMenu = found[0];
					}
					var log = XPDisplayMod.Instance.Log;
					log.LogInfo("[XP] SettingsMenuManager instances: " + found.Length + " tracking=" + _settingsMenu.gameObject.name);
					for (int i = 0; i < found.Length; i++)
					{
						log.LogInfo("[XP]   [" + i + "] root=" + found[i].gameObject.name +
							" active=" + found[i].gameObject.activeInHierarchy +
							" parent=" + (found[i].transform.parent != null ? found[i].transform.parent.gameObject.name : "none"));
					}
				}
			}

			// one-time inventory: every UI rect whose name smells like a settings screen
			if (!_scanLogged)
			{
				_scanLogged = true;
				try
				{
					RectTransform[] all = UnityEngine.Object.FindObjectsOfType<RectTransform>(true);
					XPDisplayMod.Instance.Log.LogInfo("[XP] rect inventory size: " + all.Length);
					for (int i = 0; i < all.Length; i++)
					{
						string n = all[i].gameObject.name;
						if (n != null && (n.IndexOf("etting", StringComparison.OrdinalIgnoreCase) >= 0 ||
							n.IndexOf("ettings", StringComparison.OrdinalIgnoreCase) >= 0))
						{
							XPDisplayMod.Instance.Log.LogInfo("[XP] candidate: '" + n + "' active=" + all[i].gameObject.activeInHierarchy +
								" parent=" + (all[i].parent != null ? all[i].parent.gameObject.name : "none"));
						}
					}
				}
				catch (Exception ex)
				{
					XPDisplayMod.Instance.Log.LogError("[XP] inventory failed: " + ex.Message);
				}
			}

			// one-shot dump + live injection happen while the settings panel is OPEN.
			// in-game the visible settings screen is XboxSettingMenu (no "Menu" child),
			// so fall back to the tracked instance root being active in the hierarchy.
			Transform menu = _settingsMenu != null ? FindChild(_settingsMenu.transform, "Menu") : null;
			bool openNow = _settingsMenu != null && (menu != null
				? menu.gameObject.activeInHierarchy
				: _settingsMenu.gameObject.activeInHierarchy);
			if (openNow && !_settingsDumpDone)
			{
				DumpSettingsScreen();
			}
			if (openNow && menu != null)
			{
				TryInjectSettingsButton(menu);
				EnforceStrip();
				HitTestCursor();
			}
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("[XP] scan failed: " + ex.Message);
		}
	}

	// one-shot DEV dump: writes the game's settings-screen hierarchy + every button
	// with its visible text to the BepInEx log so we can pick the exact button to clone
	private int _injectTries;

	private void TryInjectSettingsButton(Transform menu)
	{
		if (_injectTries >= 60) return;
		try
		{
			// fresh settings instances are created each open - re-inject if the tab is missing
			if (FindChild(menu, "XPDisplayTabButton") != null)
			{
				Transform host = FindTabHost(menu);
				if (host != null) BuildEnforceStrip(host);
				return;
			}
			_injectTries++;

			Transform buttonsHost = FindTabHost(menu);
			if (buttonsHost == null) return;

			// log every direct child of the strip so we can verify placement
			for (int i = 0; i < buttonsHost.childCount; i++)
			{
				Transform child = buttonsHost.GetChild(i);
				RectTransform cr = child.GetComponent<RectTransform>();
				XPDisplayMod.Instance.Log.LogInfo("[XP]   strip child[" + i + "] '" + child.gameObject.name +
					"' size=" + cr.sizeDelta + " pos=" + cr.anchoredPosition);
			}

			Transform templ = FindChild(buttonsHost, "Gameplay Tab Button");
			if (templ == null) templ = FindChild(buttonsHost, "Audio Tab Button");
			if (templ == null) return;

			// remove the previous full-width-row experiment if any
			Transform oldRow = FindChild(menu, "XPDisplaySettingsRow");
			if (oldRow != null) UnityEngine.Object.Destroy(oldRow.gameObject);

			GameObject clone = UnityEngine.Object.Instantiate(templ.gameObject);
			clone.name = "XPDisplayTabButton";
			clone.transform.SetParent(buttonsHost, false);

			// replace the TMP label with our own legacy Text label
			for (int i = clone.transform.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.Destroy(clone.transform.GetChild(i).gameObject);
			}
			var labelGo = new GameObject("XPDisplayLabel", Il2CppType.Of<RectTransform>(), Il2CppType.Of<UnityEngine.UI.Text>());
			labelGo.transform.SetParent(clone.transform, false);
			RectTransform lrt = labelGo.GetComponent<RectTransform>();
			lrt.anchorMin = Vector2.zero;
			lrt.anchorMax = Vector2.one;
			lrt.offsetMin = new Vector2(8f, 0f);
			lrt.offsetMax = new Vector2(-8f, 0f);
			UnityEngine.UI.Text txt = labelGo.GetComponent<UnityEngine.UI.Text>();
			txt.text = "XP DISPLAY";
			txt.fontSize = 17;
			txt.alignment = TextAnchor.MiddleCenter;
			txt.color = Color.white;
			txt.fontStyle = FontStyle.Bold;
			// match the game's look: builtin Arial is the only rasterizing font in IL2CPP
			try
			{
				txt.font = LoadTabFont();
			}
			catch (Exception)
			{
				txt.font = null;
			}
			// native tab text has a subtle 1px drop shadow - replicate with UGUI Shadow
			try
			{
				UnityEngine.UI.Shadow sh = clone.AddComponent(Il2CppType.Of<UnityEngine.UI.Shadow>()).Cast<UnityEngine.UI.Shadow>();
				sh.effectColor = new Color(0f, 0f, 0f, 0.35f);
				sh.effectDistance = new Vector2(1f, -1f);
			}
			catch (Exception)
			{
			}

			UnityEngine.UI.Button btn = clone.GetComponent<UnityEngine.UI.Button>();
			if (btn == null)
			{
				btn = clone.AddComponent(Il2CppType.Of<UnityEngine.UI.Button>()).Cast<UnityEngine.UI.Button>();
			}
			btn.onClick.RemoveAllListeners();
			btn.onClick.AddListener((UnityEngine.Events.UnityAction)OnModsButtonClicked);
			XPDisplayMod.Instance.Log.LogInfo("[XP] listener attached to XPDisplayTabButton (active=" +
				clone.activeInHierarchy + " parent=" + clone.transform.parent.name + ")");

			clone.transform.SetAsLastSibling();
			clone.SetActive(true);
			BuildEnforceStrip(buttonsHost);
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("[XP] inject try " + _injectTries + " failed: " + ex.Message);
		}
	}

	private Transform FindTabHost(Transform menu)
	{
		// scan ALL objects named "Buttons" under the settings screen and pick the one
		// that actually contains the tab buttons (there are several 'Buttons' groups)
		var hosts = new System.Collections.Generic.List<Transform>();
		CollectNamed(menu, "Buttons", hosts);
		for (int h = 0; h < hosts.Count; h++)
		{
			if (FindChild(hosts[h], "Gameplay Tab Button") != null ||
				FindChild(hosts[h], "Audio Tab Button") != null)
			{
				XPDisplayMod.Instance.Log.LogInfo("[XP] tab strip found: '" + hosts[h].gameObject.name +
					"' path=" + FindPath(hosts[h]) + " children=" + hosts[h].childCount);
				return hosts[h];
			}
		}
		return null;
	}

	// the game hardcodes tab positions on a fixed pitch (measured 260px, first tab at 392).
	// A 6th tab doesn't fit that pitch, so we re-space ALL tabs evenly across the SAME total
	// strip width (tabs stay 200px wide, gaps tighten a little) and enforce the layout every
	// frame while the settings screen is open, so the game's own script can't snap it back.
	private const int MaxEnforce = 8;
	private int _enforceCount;
	private RectTransform[] _enforceRts = new RectTransform[MaxEnforce];
	private Vector2[] _enforcePos = new Vector2[MaxEnforce];
	private Vector2[] _enforceSize = new Vector2[MaxEnforce];

	private void EnforceStrip()
	{
		if (_enforceCount <= 0) return;
		try
		{
			for (int i = 0; i < _enforceCount; i++)
			{
				RectTransform rt = _enforceRts[i];
				if (rt == null) continue;
				if ((rt.anchoredPosition - _enforcePos[i]).sqrMagnitude > 0.01f)
				{
					rt.anchoredPosition = _enforcePos[i];
				}
				if (rt.sizeDelta != _enforceSize[i])
				{
					rt.sizeDelta = _enforceSize[i];
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private void OnModsButtonClicked()
	{
		XPDisplayMod.Instance.Log.LogInfo("[XP] tab clicked: active=" + gameObject.activeInHierarchy +
			" settings=" + (_settingsMenu != null && _settingsMenu.gameObject.activeInHierarchy));
		// behaves like a settings tab: opens our config section as an overlay ABOVE the
		// window (UGUI canvas) - the settings screen stays open underneath
		ModsCfgPanel.Open();
		XPDisplayMod.Instance.Log.LogInfo("[XP] settings button clicked -> opening mod screen");
	}

	// one-shot debug: what UI sits under the mouse when the user clicks while settings is open
	private float _nextHitTest = 0f;
	private void HitTestCursor()
	{
		if (Time.unscaledTime < _nextHitTest) return;
		_nextHitTest = Time.unscaledTime + 0.4f;
		try
		{
			if (!Input.GetMouseButtonDown(0)) return;
			var es = UnityEngine.EventSystems.EventSystem.current;
			if (es == null) return;
			var ped = new UnityEngine.EventSystems.PointerEventData(es);
			ped.position = Input.mousePosition;
			var hits = new Il2CppSystem.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
			es.RaycastAll(ped, hits);
			if (hits.Count == 0)
			{
				XPDisplayMod.Instance.Log.LogInfo("[XP] mouse-hit: NOTHING");
				return;
			}
			string top = hits[0].gameObject != null ? hits[0].gameObject.name : "?";
			string ours = "";
			for (int i = 0; i < hits.Count; i++)
			{
				if (hits[i].gameObject != null && hits[i].gameObject.name == "XPDisplayTabButton")
				{
					ours = " (OUR TAB in hits[" + i + "])";
				}
			}
			var btn = hits[0].gameObject != null ? hits[0].gameObject.GetComponent<UnityEngine.UI.Button>() : null;
			XPDisplayMod.Instance.Log.LogInfo("[XP] mouse-hit: top='" + top + "' hits=" + hits.Count +
				" button-top=" + (btn != null) + ours);
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogInfo("[XP] hit-test failed: " + ex.Message);
		}
	}

	// one-shot DEV dump: shows the settings hierarchy for debugging
	private void DumpSettingsScreen()
	{
		if (_settingsDumpDone) return;
		_settingsDumpDone = true;
		try
		{
			var log = XPDisplayMod.Instance.Log;
			log.LogInfo("=== [XP] Settings screen tree ===");
			DumpTransform(_settingsMenu.transform, 0, log);
			UnityEngine.UI.Button[] btns = _settingsMenu.GetComponentsInChildren<UnityEngine.UI.Button>(true);
log.LogInfo("=== [XP] Buttons: " + btns.Length);
			for (int i = 0; i < btns.Length; i++)
			{
				UnityEngine.UI.Button b = btns[i];
				string text = GetChildText(b.transform);
				log.LogInfo("  [" + i + "] " + b.gameObject.name + "  text='" + text + "'  parent=" + b.transform.parent.name);
			}
			try
			{
				UnityEngine.UI.Slider[] sliders = _settingsMenu.GetComponentsInChildren<UnityEngine.UI.Slider>(true);
				log.LogInfo("=== [XP] Sliders: " + sliders.Length);
				for (int i = 0; i < sliders.Length; i++)
				{
					log.LogInfo("  [" + i + "] " + sliders[i].gameObject.name + " path=" + FindPath(sliders[i].transform));
				}
			}
			catch (Exception ex)
			{
				log.LogInfo("[XP] slider list failed: " + ex.Message);
			}
			try
			{
				UnityEngine.UI.Toggle[] toggles = _settingsMenu.GetComponentsInChildren<UnityEngine.UI.Toggle>(true);
				log.LogInfo("=== [XP] Toggles: " + toggles.Length);
				for (int i = 0; i < toggles.Length; i++)
				{
					log.LogInfo("  [" + i + "] " + toggles[i].gameObject.name + " path=" + FindPath(toggles[i].transform));
				}
			}
			catch (Exception ex)
			{
				log.LogInfo("[XP] toggle list failed: " + ex.Message);
			}
			log.LogInfo("=== [XP] Node inspector ===");
			DumpNode(_settingsMenu.transform, "Language");
			DumpNode(_settingsMenu.transform, "MasterVolume");
			DumpNode(_settingsMenu.transform, "MasterVolumeSettings");
			DumpNode(_settingsMenu.transform, "SFXVolume");
			DumpNode(_settingsMenu.transform, "SFXVolumeSettings");
			DumpNode(_settingsMenu.transform, "VibrationStrength");
			DumpNode(_settingsMenu.transform, "InterfaceButton");
			DumpNode(_settingsMenu.transform, "Save Button");
			DumpNode(_settingsMenu.transform, "Back Button");
			try
			{
				RectTransform[] namedRects = _settingsMenu.GetComponentsInChildren<RectTransform>(true);
				int matchCount = 0;
				for (int i = 0; i < namedRects.Length; i++)
				{
					string n2 = namedRects[i].gameObject.name;
					if (n2 == null) continue;
					bool hit = n2.IndexOf("Toggle", StringComparison.OrdinalIgnoreCase) >= 0 ||
						n2.IndexOf("Slider", StringComparison.OrdinalIgnoreCase) >= 0 ||
						n2.IndexOf("Checkbox", StringComparison.OrdinalIgnoreCase) >= 0 ||
						n2.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) >= 0 ||
						n2.IndexOf("Background", StringComparison.OrdinalIgnoreCase) >= 0;
					if (hit)
					{
						DumpNodeTransform(namedRects[i], n2 + " [" + matchCount + "]");
						matchCount++;
					}
				}
				log.LogInfo("=== [XP] Matched-name node dumps: " + matchCount);
			}
			catch (Exception ex)
			{
				log.LogInfo("[XP] matched-name scan failed: " + ex.Message);
			}
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("[XP] settings dump failed: " + ex);
		}
	}

	// loads the tab label font. IL2CPP strips Font.CreateDynamicFontFromOSFont and native
// resource Fonts never rasterize, so the ONLY renderable path is the builtin Arial.
	private static Font LoadTabFont()
	{
		try
		{
			Font res = Resources.GetBuiltinResource<Font>("Arial.ttf");
			if (res != null)
			{
				XPDisplayMod.Instance.Log.LogInfo("[XP] tab font: builtin Arial");
				return res;
			}
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogInfo("[XP] tab font builtin Arial failed: " + ex.Message);
		}
		return null;
	}

	private static string[] NewNativeFontNames()
	{
		// kept for reference - CreateDynamicFontFromOSFont is stripped in this IL2CPP build
		return new[] { "Liberation Sans", "Arial", "Pedestrian" };
	}

	private const float PitchStep = 216.6667f; // tabs 200 wide, ~16.7px gaps - the look Dali approved
	private const float FittedWidth = 200f;

	private void BuildEnforceStrip(Transform buttonsHost)
	{
		try
		{
			int n = buttonsHost.childCount;
			if (n < 2 || n > MaxEnforce) return;
			float firstX = float.MaxValue;
			float baseY = 0f;
			for (int i = 0; i < n; i++)
			{
				RectTransform rt = buttonsHost.GetChild(i).GetComponent<RectTransform>();
				float x = rt.anchoredPosition.x;
				if (i == 0) baseY = rt.anchoredPosition.y;
				if (x < firstX) firstX = x;
			}

			// even 260-ish native pitch tuned to fit 6 tabs inside the window: no per-scan
			// widening (that accumulated +48/frame and drifted the strip off-screen)
			_enforceCount = n;
			for (int i = 0; i < n; i++)
			{
				RectTransform rt = buttonsHost.GetChild(i).GetComponent<RectTransform>();
				_enforceRts[i] = rt;
				_enforceSize[i] = new Vector2(FittedWidth, rt.sizeDelta.y);
				_enforcePos[i] = new Vector2(firstX + i * PitchStep, baseY);
				rt.anchoredPosition = _enforcePos[i];
				rt.sizeDelta = _enforceSize[i];
			}
			XPDisplayMod.Instance.Log.LogInfo("[XP] strip re-spaced: " + n + " tabs, width=" + FittedWidth +
				" pitch=" + PitchStep + " range=" + firstX + "->" + (firstX + (n - 1) * PitchStep + FittedWidth));
		}
		catch (Exception)
		{
		}
	}

	private static void CollectNamed(Transform root, string name, System.Collections.Generic.List<Transform> results)
	{
		if (root == null) return;
		if (root.gameObject.name == name) results.Add(root);
		for (int i = 0; i < root.childCount; i++)
		{
			CollectNamed(root.GetChild(i), name, results);
		}
	}

	private static string FindPath(Transform t)
	{
		if (t == null) return "";
		return t.parent != null ? FindPath(t.parent) + "/" + t.gameObject.name : t.gameObject.name;
	}

	// one-shot: native XP/level/progress/store HUD component scan (data gathering)
	private void ScanNativeXpNodes()
	{
		if (_nativeXpScanDone) return;
		_nativeXpScanDone = true;
		try
		{
			RectTransform[] all = UnityEngine.Object.FindObjectsOfType<RectTransform>(true);
			XPDisplayMod.Instance.Log.LogInfo("[XP] native-XP scan: " + all.Length + " rects");
			for (int i = 0; i < all.Length; i++)
			{
				string n = all[i].gameObject.name;
				if (n != null && (n.IndexOf("xp", StringComparison.OrdinalIgnoreCase) >= 0 ||
					n.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0 ||
					n.IndexOf("exp", StringComparison.OrdinalIgnoreCase) >= 0 ||
					n.IndexOf("progress", StringComparison.OrdinalIgnoreCase) >= 0 ||
					n.IndexOf("store", StringComparison.OrdinalIgnoreCase) >= 0))
				{
					XPDisplayMod.Instance.Log.LogInfo("[XP] native-XP candidate: '" + n + "' parent=" +
						(all[i].parent != null ? all[i].parent.gameObject.name : "none") + " path=" + FindPath(all[i]));
				}
			}
			XPDisplayMod.Instance.Log.LogInfo("[XP] native-XP scan done");
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("[XP] native-XP scan failed: " + ex.Message);
		}
	}

	private static Transform FindChild(Transform root, string name)
	{
		if (root == null) return null;
		if (root.gameObject.name == name) return root;
		for (int i = 0; i < root.childCount; i++)
		{
			Transform r = FindChild(root.GetChild(i), name);
			if (r != null) return r;
		}
		return null;
	}

	private static void DumpNode(Transform root, string name)
	{
		Transform t = FindChild(root, name);
		if (t == null)
		{
			XPDisplayMod.Instance.Log.LogInfo("[XP] node '" + name + "' NOT FOUND");
			return;
		}
		DumpNodeTransform(t, name);
	}

	private static void DumpNodeTransform(Transform t, string label)
	{
		var log = XPDisplayMod.Instance.Log;
		RectTransform rect = t.GetComponent<RectTransform>();
		if (rect != null)
		{
			log.LogInfo("[XP] '" + label + "' Rect size=" + rect.sizeDelta + " pos=" + rect.anchoredPosition +
				" anchor=" + rect.anchorMin + "->" + rect.anchorMax + " pivot=" + rect.pivot + " active=" + t.gameObject.activeInHierarchy);
		}
		else
		{
			log.LogInfo("[XP] '" + label + "' Transform localPos=" + t.localPosition + " active=" + t.gameObject.activeInHierarchy);
		}
		UnityEngine.UI.Text txtComp = t.GetComponent<UnityEngine.UI.Text>();
		if (txtComp != null)
		{
			log.LogInfo("[XP]   text: '" + txtComp.text + "' font=" + txtComp.font.name + " size=" + txtComp.fontSize +
				" color=" + txtComp.color + " align=" + txtComp.alignment + " bold=" + txtComp.fontStyle);
		}

		// component detail pass (data gathering for native-UI cloning)
		try
		{
			UnityEngine.UI.Image img = t.GetComponent<UnityEngine.UI.Image>();
			if (img != null)
			{
				string spriteName = "null";
				try { if (img.sprite != null) spriteName = img.sprite.name; } catch (Exception) { }
				string typeStr = "?";
				try
				{
					if (img.type == UnityEngine.UI.Image.Type.Simple) typeStr = "Simple";
					else if (img.type == UnityEngine.UI.Image.Type.Sliced) typeStr = "Sliced";
					else if (img.type == UnityEngine.UI.Image.Type.Tiled) typeStr = "Tiled";
					else if (img.type == UnityEngine.UI.Image.Type.Filled) typeStr = "Filled";
				}
				catch (Exception) { }
				log.LogInfo("[XP]   image: sprite=" + spriteName + " type=" + typeStr +
					" color=" + img.color.r + "," + img.color.g + "," + img.color.b + "," + img.color.a +
					" fillCenter=" + img.fillCenter + " ppu=" + img.pixelsPerUnitMultiplier);
			}
		}
		catch (Exception) { }

		try
		{
			TMPro.TextMeshProUGUI tmp = t.GetComponent<TMPro.TextMeshProUGUI>();
			if (tmp != null)
			{
				string fontName = "null";
				try { if (tmp.font != null) fontName = tmp.font.name; } catch (Exception) { }
				string fstyle = "?";
				try
				{
					if (tmp.fontStyle == TMPro.FontStyles.Normal) fstyle = "Normal";
					else fstyle = tmp.fontStyle.ToString();
				}
				catch (Exception)
				{
					fstyle = "?";
				}
				log.LogInfo("[XP]   tmp: font=" + fontName + " size=" + tmp.fontSize +
					" color=" + tmp.color.r + "," + tmp.color.g + "," + tmp.color.b + "," + tmp.color.a +
					" style=" + fstyle);
			}
		}
		catch (Exception) { }

		try
		{
			UnityEngine.UI.Toggle tg = t.GetComponent<UnityEngine.UI.Toggle>();
			if (tg != null)
			{
				string targetName = "null", graphicName = "null";
				try { if (tg.targetGraphic != null) targetName = tg.targetGraphic.gameObject.name; } catch (Exception) { }
				try { if (tg.graphic != null) graphicName = tg.graphic.gameObject.name; } catch (Exception) { }
				log.LogInfo("[XP]   toggle: isOn=" + tg.isOn + " targetGraphic=" + targetName + " checkmark=" + graphicName);
			}
		}
		catch (Exception) { }

		try
		{
			UnityEngine.UI.Slider sl = t.GetComponent<UnityEngine.UI.Slider>();
			if (sl != null)
			{
				string fillName = "null", handleName = "null", backgroundName = "none";
				try { if (sl.fillRect != null) fillName = sl.fillRect.name; } catch (Exception) { }
				try { if (sl.handleRect != null) handleName = sl.handleRect.name; } catch (Exception) { }
				try
				{
					for (int c = 0; c < t.childCount; c++)
					{
						if (t.GetChild(c).gameObject.name.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							backgroundName = t.GetChild(c).gameObject.name;
							break;
						}
					}
				}
				catch (Exception) { }
				log.LogInfo("[XP]   slider: min=" + sl.minValue + " max=" + sl.maxValue + " whole=" + sl.wholeNumbers +
					" fillRect=" + fillName + " handleRect=" + handleName + " background=" + backgroundName);
			}
		}
		catch (Exception) { }

		try
		{
			Component[] comps = t.GetComponents<Component>();
			string list = "";
			for (int i = 0; i < comps.Length; i++)
			{
				try
				{
					list += comps[i].GetIl2CppType().FullName + "; ";
				}
				catch (Exception)
				{
				}
			}
			log.LogInfo("[XP]   comps: " + list);
		}
		catch (Exception) { }

		log.LogInfo("[XP]   children: ");
		for (int i = 0; i < t.childCount; i++)
		{
			log.LogInfo("[XP]     - " + t.GetChild(i).gameObject.name);
		}
	}

	private static string GetChildText(Transform root)
	{
		UnityEngine.UI.Text t = root.GetComponentInChildren<UnityEngine.UI.Text>(true);
		return t != null ? t.text : "";
	}

	private static void DumpTransform(Transform t, int depth, BepInEx.Logging.ManualLogSource log)
	{
		if (t == null || depth > 7) return;
		string label = "";
		UnityEngine.UI.Text txt = t.GetComponent<UnityEngine.UI.Text>();
		if (txt != null) label = "  [Text:'" + txt.text + "']";
		log.LogInfo(new string(' ', depth * 2) + t.gameObject.name + label);
		for (int i = 0; i < t.childCount; i++)
		{
			DumpTransform(t.GetChild(i), depth + 1, log);
		}
	}

	private void OnManagerAcquired()
	{
		try
		{
			_level = _manager.CurrentLevel;
			_isLast = _manager.IsLastLevel;
			_maxLevels = 1;
			_prevRequirement = 0;

			// read the full level curve (the game's own table)
			Il2CppReferenceArray<StoreLevelManager.LevelRequirement> table = _manager.m_LevelRequirements;
			if (table != null && table.Length > 0)
			{
				_maxLevels = table.Length;
				int idx = Mathf.Clamp(_level - 1, 0, table.Length - 1);
				_prevRequirement = table[idx].RequiredPoints;
			}

			// first sanity check: is XP cumulative (CurrentPoint keeps growing) or per-level?
			_currentXp = _manager.CurrentPoint;
			_requiredXp = _manager.NextLevelRequirement;
			_branchCumulative = _currentXp > _requiredXp;

			SyncLevelState();
			Subscribe();
			ScanNativeXpNodes();
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("XPDisplay: failed to read StoreLevelManager: " + ex);
			_manager = null;
		}
	}

	private void Subscribe()
	{
		if (_subscribed || _manager == null) return;
		_subscribed = true;
		try
		{
			// the game's own StorePointSlider hooks the exact same events
			// (interop events are Il2CppSystem.Action - explicit cast = method group conversion)
			_manager.onPointChanged += (Il2CppSystem.Action<int, bool>)OnPointChanged;
			_manager.onLevelChanged += (Il2CppSystem.Action<bool>)OnLevelChanged;
			_manager.onDisabled += (Il2CppSystem.Action)OnDisabled;
		}
		catch (Exception ex)
		{
			XPDisplayMod.Instance.Log.LogError("XPDisplay: subscribe failed: " + ex);
			_subscribed = false;
		}
	}

	private void Unsubscribe()
	{
		if (!_subscribed || _manager == null) return;
		_subscribed = false;
		try
		{
			_manager.onPointChanged -= (Il2CppSystem.Action<int, bool>)OnPointChanged;
			_manager.onLevelChanged -= (Il2CppSystem.Action<bool>)OnLevelChanged;
			_manager.onDisabled -= (Il2CppSystem.Action)OnDisabled;
		}
		catch (Exception)
		{
			// game shutting down - ignore
		}
	}

	private void OnPointChanged(int amount, bool increased)
	{
		if (!increased)
		{
			// a purchase/restock consumed XP (same signal the game's bar listens to)
			amount = -Mathf.Abs(amount);
		}
		_sessionXp += amount;

		if (_manager == null) return;
		_currentXp = _manager.CurrentPoint;
		_requiredXp = _manager.NextLevelRequirement;
		_isLast = _manager.IsLastLevel;
		SyncLevelState();

		if (XPDisplayMod.ShowFloaters.Value && amount != 0)
		{
			AddFloater((amount > 0 ? "+" : "") + amount + " XP", amount > 0,
				new Color(0.33f, 0.85f, 0.42f, 1f), new Color(0.9f, 0.32f, 0.32f, 1f));
		}
	}

	private void OnLevelChanged(bool levelUp)
	{
		_levelUps++;
		if (_manager == null) return;
		_level = _manager.CurrentLevel;
		_isLast = _manager.IsLastLevel;
		_currentXp = _manager.CurrentPoint;
		_requiredXp = _manager.NextLevelRequirement;
		SyncLevelState();

		if (!levelUp) return;

		float reward = 0f;
		if (XPDisplayMod.RewardEnabled.Value)
		{
			reward = TryGrantReward();
		}

		if (XPDisplayMod.ShowPopup.Value && _popupStart < Time.unscaledTime - 1f)
		{
			_popupStart = Time.unscaledTime;
			if (_isLast)
			{
				_popupTitle = "MAX LEVEL!";
				_popupSub = reward > 0f ? "Everything unlocked  +$" + (int)reward : "Everything unlocked";
			}
			else
			{
				_popupTitle = "STORE LEVEL " + _level + "!";
				_popupSub = reward > 0f ? "New products  +$" + (int)reward : "New products unlocked";
			}
		}

		// the game's own money ticker shows the reward, but also float it on the HUD
		if (reward > 0f && XPDisplayMod.ShowFloaters.Value)
		{
			AddFloater("+$" + (int)reward, true, XPDisplayMod.AccentCol, XPDisplayMod.AccentCol);
		}
	}

	private bool _rewardFailedLogged;

	private float TryGrantReward()
	{
		float amount = Mathf.Max(0f, XPDisplayMod.RewardPerLevel.Value);
		if (amount <= 0f) return 0f;
		try
		{
			if (!NoktaSingleton<MoneyManager>.HasInstance)
			{
				return 0f;
			}
			MoneyManager mm = NoktaSingleton<MoneyManager>.Instance;
			mm.MoneyTransition(amount, MoneyManager.TransitionType.CHECKOUT_INCOME, true);
			return amount;
		}
		catch (Exception ex)
		{
			if (!_rewardFailedLogged)
			{
				XPDisplayMod.Instance.Log.LogError("XPDisplay: level-up reward failed (feature disabled): " + ex);
				_rewardFailedLogged = true;
			}
			return 0f;
		}
	}

	private void OnDisabled()
	{
		_visible = false;
		_floaters.Clear();
	}

	private void SyncLevelState()
	{
		if (_isLast)
		{
			_fillTarget = 1f;
			return;
		}

		if (_branchCumulative && _requiredXp > _prevRequirement)
		{
			// cumulative curve: progress inside the current level slice
			_fillTarget = Mathf.Clamp01((float)(_currentXp - _prevRequirement) / (float)(_requiredXp - _prevRequirement));
		}
		else
		{
			// per-level reset: direct ratio
			_fillTarget = _requiredXp > 0 ? Mathf.Clamp01((float)_currentXp / (float)_requiredXp) : 1f;
		}
	}

	private void AddFloater(string text, bool positive, Color good, Color bad)
	{
		_floaters.Add(new Floater { Text = text, Color = positive ? good : bad, Start = Time.unscaledTime });
		if (_floaters.Count > MaxFloaters)
		{
			_floaters.RemoveAt(0);
		}
	}

	public string LevelLine()
	{
		if (_isLast)
		{
			return "LEVEL " + _level + "  /  MAX";
		}
		return "LEVEL " + _level + " / " + _maxLevels;
	}

	public string XpLine()
	{
		if (_isLast)
		{
			return _currentXp + " XP  MAX LEVEL";
		}
		return _currentXp + " / " + _requiredXp + " XP";
	}

	public string StatsLine()
	{
		float elapsed = Mathf.Max(0.001f, Time.unscaledTime - _sessionStart);
		int xpPerHour = (int)(_sessionXp / elapsed * 3600f);
		return "Session: " + _sessionXp + " XP  ~" + xpPerHour + "/h  " + _levelUps + " levels";
	}

	public float FillShown => _fillShown;
	public bool Visible => _visible;
	public bool Ready => _manager != null;
	public float PopupStart => _popupStart;
	public string PopupTitle => _popupTitle;
	public string PopupSub => _popupSub;
	public List<Floater> Floaters => _floaters;

	private static KeyCode ParseKey(string name, KeyCode fallback)
	{
		if (string.IsNullOrEmpty(name)) return fallback;
		try
		{
			return (KeyCode)Enum.Parse(typeof(KeyCode), name, true);
		}
		catch (Exception)
		{
			return fallback;
		}
	}

	private void OnGUI()
	{
		// XP config screen is IMGUI (the SAME pipeline as the HUD bar, floaters, popup
		// and the tab labels you approved - all render in this build). IMGUI draws AFTER
		// the UGUI settings window, so the panel appears ABOVE it exactly like any other
		// settings sub-screen, and we do NOT need to hide/restore anything.
		if (ModsCfgPanel.IsOpen)
		{
			ModsCfgPanel.Render();
			return;
		}
		XpHudRenderer.Render(this);
	}

	private void OnDestroy()
	{
		Unsubscribe();
	}

	private void OnApplicationQuit()
	{
		Unsubscribe();
	}
}
```

