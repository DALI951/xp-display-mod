# XPDisplayMod - AI Handoff Context

This file is the single source of truth for any AI/agent (or human) picking up this
project cold. Read this WHOLE file before touching any code. It explains what the mod
IS, what the GOAL is, what has been PROVEN to work in this specific game build, and what
is still left to do. It exists so a brand-new session can understand everything from this
file alone.

---

## 1. What this is

A BepInEx **IL2CPP** plugin for **Supermarket Simulator v1.6.0 (all DLC), the SteamUnlocked
release** that runs under **BepInEx 6 IL2CPP** with the Unity engine assemblies in the
winding `BepInEx/interop` folder. The mod draws an **XP / level display** overlay on the
main game screen:

- a top **HUD bar** (gold fill) showing earned XP
- **XP floaters** that drift up when XP is gained
- a **level-up popup** (big gold title + sub line)
- a **session stats line** (XP per hour, levels gained)
- a **settings-style tab** named `XP DISPLAY` appended to the game's own tab strip, with a
  gold label
- a **config panel** that is SUPPOSED to open when the player clicks that tab (like a native
  in-game settings sub-screen) - THIS IS THE PIECE THAT IS CURRENTLY IN PROGRESS.

The plugin class is `XPDisplayBehaviour : BaseUnityPlugin` (IL2CPP base). Its DLL
`XPDisplayMod.dll` lives in the Release build folder; copy it into the game's
`BepInEx/plugins/` folder to install.

---

## 2. THE GOAL (what "done" looks like)

Make the XP Display mod feel and look like it is a **native in-game feature** - not a
hacked-on overlay:

1. The **`XP DISPLAY` tab** in the game's tab strip must open a **config screen** when
   clicked, exactly like the game's own settings sub-screens.
2. That config screen must:
   - look like the rest of the game's settings UI (dark panel, gold accent, readable
     labels) - the game uses a dark theme with a gold/amber accent `#FFDA60`.
   - let the player toggle the HUD bar, popup, floaters, session stats, rewards; adjust
     scale; pick XP colors; change the toggle key; save to BepInEx config.
   - stay on top of the game's settings window (draw AFTER it, like any settings sub-screen)
   - NOT fight with / hide the underlying settings screen. It behaves like a focused sub-screen.
3. Every setting actually does something (toggles HUD parts, changes scale/colors, etc.)

**Acceptance test (what the user does to check):**
- Start game -> XP bar, floaters, popup render (PROVEN).
- Click the `XP DISPLAY` tab at the top -> a settings panel must appear over the screen
  (THIS IS THE CURRENT GAP).
- Toggle things in the panel -> the HUD changes immediately.
- Press F2 (config toggle key) -> panel toggles open/closed.

---

## 3. THE GAME + TOOLCHAIN (all paths, exact)

- Game root: `C:\SteamUnlocked\Supermarket.Sim.v1.6.0.ALL.DLC\Supermarket.Sim.v1.6.0.ALL.DLC\`
  (the game folder is repeated; the inner one holds the executable).
- **ALT/GAME-CFG NOTE:** there is ALSO a copy of the game at
  `F:\mods\supermarket\xp\` (folder `supermarket`) - the source is authored under
  `F:\mods\supermarket\xp\XPDisplayMod\` and built to `XPDisplayMod\bin\Release\`.
- Game executable: `Supermarket Simulator.exe` in that root.
- BepInEx config: `BepInEx/plugins/` (put the built DLL here), log at `BepInEx/LogOutput.log`
  (or `LogOutput.log`). Interop assemblies at `BepInEx/interop/`.
- IL2CPP: the game is IL2CPP-compiled, so source is NOT decompilable to normal C#; the
  interop folder has the `Il2Cpp*` and `Unity* .dll` API surface used to write plugins.

**Compiler / build (CRITICAL - do it EXACTLY this way):**

```
F:\mods\supermarket\xp\XPDisplayMod\XPDisplayMod.csproj
```

Build with the framework MSBuild + the **Roslyn** compiler that matches the currently
installed one. Common working invocation (from the project dir):

```powershell
$msb = "$env:windir\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
$roslyn = "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn"
& $msb .\XPDisplayMod.csproj /t:Build /p:Configuration=Release /v:q /nologo "/p:CscToolPath=$roslyn" /p:CscToolExe=csc.exe
```

If `CscToolPath` is wrong the build will fail weirdly; point it to where `csc.exe` actually is.

**Assembly references in the csproj** point at the game's interop DLLs (UnityEngine.*,
BepInEx.*, Il2CppSystem.*, XPDisplayMod.csproj itself). The DLL that the game loads is:
`XPDisplayMod\bin\Release\XPDisplayMod.dll` (~43 KB). Deploy = copy that DLL into the game
`BepInEx/plugins/`.

---

## 4. ENGINE RULES - WHAT IS PROVEN vs DEAD (MOST IMPORTANT SECTION)

This section exists because several "obvious" Unity UI approaches FAIL or CRASH in this
IL2CPP build. Do not re-introduce them. Verified by actual runtime testing:

### PROVEN (use these, they work in this build):
- **IMGUI / OnGUI rendering.** `private void OnGUI() { }` in the plugin draws the HUD bar,
  floaters, popup, tab label, and any panel you draw with GUI.* primitives. IMGUI draws
  AFTER every UGUI canvas, so an IMGUI panel appears ON TOP of the game's settings screen -
  exactly like a native settings sub-screen. THIS IS THE ONLY RENDERING PATH THAT WORKS.
  - You can build IMGUI without fancy UGUI: implement the config panel with
    `GUI.DrawTexture`, `GUI.Label`, `GUI.Toggle`, `GUI.HorizontalSlider`, `GUIStyle`,
    builtin font via `Resources.GetBuiltinResource<Font>("Arial.ttf")`.
  - The mod ALREADY has a proven IMGUI HUD renderer (`XpHudRenderer`, see `XpHudRenderer.cs`):
    gold `#FFDA60` fill, dark `#222` panels, rounded `Texture2D` made by hand with
    `SetPixels`, the Arial font. COPY ITS STYLE/CONSTRUCTION for the panel so everything
    matches.
- **Tab cloning via Instantiate.** The tab is made by cloning the game's own native tab
  button (`Instantiate` of the settings tab object) - non-generic, no AddComponent, works.
- **Non-generic config access.** `XPDisplayMod.*` exposes `ConfigEntry<T>` for every setting;
  read `.Value`.

### DEAD / CRASH (DO NOT USE - they throw at runtime in this IL2CPP build):
- **Generic `GameObject.AddComponent<T>()`** -> TypeInitializationException at runtime:
  the IL2CPP generic-Interop store for stripped generic methods is missing. Using it to
  build a fresh UGUI Canvas/Text immediately crashes. This is WHY an earlier attempt showed
  an "empty panel" - the UGUI Canvas construction died.
- **Non-generic `go.AddComponent(System.Type)`** -> does not even compile clean against
  the interop (wants `Il2CppSystem.Type`).
- Any reliance on `Canvas`/`UGUI` Text/Image/RectTransform composed from scratch in C#.
  (UGUI only "works" if Unity itself creates it, e.g. by cloning - we don't do that for UI.)

**Consequence:** the config panel MUST be pure IMGUI, rendered from the SAME `OnGUI` that
draws the HUD. Nothing else.

---

## 5. CURRENT STATE + WHERE THE WORK IS

Files in this repo (all in `F:\mods\supermarket\xp\XPDisplayMod\`):

- `XPDisplayMod.csproj` - project; XML; references the interop DLLs.
- `XPDisplayMod.cs` - plugin class `XPDisplayMod`, all the static `ConfigEntry<...>` fields
  (Scale, OffsetX/Y, colors, ShowBar/ShowPopup/ShowFloaters/ShowStats, RewardEnabled/
  RewardPerLevel, ToggleKey, CfgKey, and the config-file keys). This is also where
  `XpHudRenderer` style + font helpers live alongside.
- `XPDisplayBehaviour.cs` - the `BaseUnityPlugin` behaviour: loop that tracks XP from a
  manager; session XP/level counters; `OnGUI` that calls the HUD renderer and (behind
  `if (ModsCfgPanel.IsOpen)`) the config panel render; the tab-click wiring that opens
  `ModsCfgPanel`. THE ONGUI HOOK + TAB CLICK ARE THE WIRING POINTS.
- `XpHudRenderer.cs` - PROVEN IMGUI HUD: RoundedTexture(), Font()/Style() helpers, bar,
  floaters, popup, session stats.
- `ModsCfgPanel.cs` - the config panel class (`ModsCfgPanel`): `IsOpen`, `Open()`,
  `Close()`, `Render()` (IMGUI). THIS IS THE FILE THAT READS THE CONFIG ENTRIES + DRAWS THE
  PANEL. It is currently a working minimal IMGUI skeleton - it draws the panel but does NOT
  yet have all the toggle/slider/color rows, and it is the #1 file to finish.
- `XpHudRenderer.cs` may also hold the shared helpers - read before editing.
- `bin\Release\XPDisplayMod.dll` - the last good build (~43 KB). Deploy this to the game
  `BepInEx/plugins/` to test.

**Wiring already in place (verify with grep, don't assume):**
- `XPDisplayBehaviour.OnGUI()` renders `XpHudRenderer.Render(this)` and, when
  `ModsCfgPanel.IsOpen`, calls `ModsCfgPanel.Render()`.
- Tab-click handler opens `ModsCfgPanel` (sets `IsOpen = true`).
- F2 (default toggle key) toggles `ModsCfgPanel.IsOpen`.

**What specifically remains (the actual TODO):**
1. Finish `ModsCfgPanel.cs`: build the full settings rows in IMGUI - toggles for
   ShowBar/ShowPopup/ShowFloaters/ShowStats/RewardEnabled, a scale slider (0.5-2.0),
   color rows (bg / accent / text) - each reading/writing its `XPDisplayMod.*` `ConfigEntry`
   `.Value`. Style it with the SAME gold `#FFDA60` + dark `#222` as the HUD (copy
   XpHudRenderer's texture/font/style construction so the panel looks native).
2. Make sure the tab's `onClick` actually opens the panel (re-grep the handler wiring).
3. Make sure `OnGUI` gates panel render on `ModsCfgPanel.IsOpen` (re-grep).
4. Build via the Roslyn invocation above; confirm 0 `error CS`.
5. Deploy DLL to the game `BepInEx/plugins/`, restart game, and have the user: click the
   tab -> panel appears; toggle things -> HUD reacts; F2 -> panel toggles.
6. Polish until it "looks like a real settings screen" (title, spacing, gold underline).

---

## 6. CONFIG ENTRIES (names, for panel wiring)

Exposed statically on the plugin (read/accept this so the panel compiles against them):

- `XPDisplayMod.Scale` (float ConfigEntry) - HUD scale
- `XPDisplayMod.OffsetX`, `XPDisplayMod.OffsetY` (float) - HUD offset
- `XPDisplayMod.BgColor`, `XPDisplayMod.AccentColor`, `XPDisplayMod.TextColor` (string
  "r,g,b,a") - colors
- `XPDisplayMod.ShowBar`, `XPDisplayMod.ShowPopup`, `XPDisplayMod.ShowFloaters`,
  `XPDisplayMod.ShowStats` (bool) - HUD toggles
- `XPDisplayMod.RewardEnabled`, `XPDisplayMod.RewardPerLevel` (bool/float) - reward
- `XPDisplayMod.ToggleKey` (string, e.g. "F2") - panel toggle key
- `XPDisplayMod.CfgKey` (string, e.g. "X") - config-file tab key
- Also commonly used: `XPDisplayMod.RewardEnabled`, `XPDisplayMod.RewardEnabled` style names
  may slightly differ - ALWAYS grep the XPDisplayMod.cs before referencing.

Referenced directly as e.g. `ModsCfgPanel.Render()` - keep the `IsOpen`/`Open`/`Close`/
`Render` public API stable, it is the contract everything hooks.

---

## 7. CARRY-OVER RULES FOR THE HANDOFF SESSION

- This is an IL2CPP BepInEx mod; HOW you write the mod matters (see section 4). If a UI
  approach "should work" but relies on generic AddComponent / fresh UGUI Canvas, it will
  crash in this build - use IMGUI.
- The game is the SteamUnlocked build; paths above are real. Verify with Test-Path before
  assuming.
- The user tests by launching the game (they run it themselves) and clicking through. Your
  output = clean build (0 CS errors) + deployed DLL + instructions on what to click.
- Do NOT ship credentials or tokens in any commit.
- Keep the memory-flow: this file is the context; the user keeps a running memory in
  `~/.config/opencode/memory/`. After finishing a session, append a dated entry to
  `session-log.md` and update `projects.md` (search for existing XPDisplay entry).

---

## 2026-09-24 — UI data-gathering pass (read `dump/UI-DATA-REPORT.md` FIRST)

- This session produced raw UI dumps in `dump/` (`RunA-main-menu-settings.txt` =
  the FULL main-menu settings screen dump, 121 KB; `RunB-native-xp-scan.txt` +
  `RunB-settings-instances.txt` = in-game data). The report `dump/UI-DATA-REPORT.md`
  is the source of truth for what was learned and what remains.
- KEY FACTS: in-game settings = TWO `SettingsMenuManager` instances under
  `---UI---/PopUps/Escape Menu/` — `XboxSettingMenu` (the VISIBLE one, Xbox-style)
  + `NewSettings Menu` (PC-style). Main menu = single `NewSettings Menu`. All game
  text is TMP (`TMPro.TextMeshProUGUI`, font via `tmp.font.name`). Native XP bar =
  `Store Point Slider` at `---UI---/Ingame Canvas/Store Point Slider`; day-end
  report = `Store Point Text` under `Day Cycle Canvas/Daily Statistics Screen`.
- BLOCKER (UNSURE, unsolved): the one-shot settings dump `DumpSettingsScreen()`
  NEVER fires in-game. Gate `openNow` stayed false across 3 gate variants (Menu-child
  active / tracked-instance active-root / both). No exceptions thrown — the scan
  just never sees the settings as "open". Escape Menu settings toggle visibility
  through something `activeInHierarchy` sampling misses (Canvas.enabled? parent
  activeSelf? timing?). Next step: log EVERY scan tick's instance states, or hook
  the settings-open, before doing ANY injection work. Main-menu settings dump works
  (Run A captured it). Tab-button injection is gated on the same `openNow` +
  PC-layout `Menu` child, so no tab appears in-game (Dali confirmed).
- Build: unchanged recipe (framework MSBuild + Roslyn override), 4 clean builds
  this session, 0 error CS. `Unity.TextMeshPro` reference added to csproj.
- NO panel/toggle/slider clone code was written this pass — data gathering only.

---

_Generated as the single-source handoff for XPDisplayMod. Whenever editing this mod, read
this file first, then the 4 source files, then the csproj, then build._
