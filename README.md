# XP Display Mod (Supermarket Simulator, IL2CPP / BepInEx 6)

**THIS IS THE AI HANDOFF REPO — the single source of truth for continuing this mod.**

Read **`AI-CONTEXT.md` at the repo root FIRST** — it contains the full engine knowledge
(what works / what is proven / what crashes in this IL2CPP build, the IMGUI-only hard
rule, config entry names, the build recipe, deployment steps, current status, and the
open TODO). This README is the quick 60-second summary + links so you land fast.

---

## What this is (60 seconds)

A BepInEx 6 IL2CPP plugin (`XPDisplayMod.dll`) for **Supermarket Simulator v1.6.0
(SteamUnlocked ALL.DLC build, game version reported as v1.6.0(223))** that shows:
- a gold **XP HUD bar** (top-right, styled to match the game's native HUD)
- **+XP / -XP floaters** that drift up on xp events
- **level-up popup** (big gold banner, scaled + faded)
- **session stats line** (XP this session, XP/hour, level-ups)
- a **"XP DISPLAY" tab** appended to the game's own tab strip (cloned from the game's
  native tab, gold label) — **clicking the tab opens a settings config panel**
- drag-to-move HUD (mouse-drag the panel top bar, auto-saves OffsetX/OffsetY to cfg)

The current engineering **hard rule**: this IL2CPP build has **stripped generic
`AddComponent<T>()`** (throws `TypeInitializationException` at runtime) — all rendering
must be **IMGUI (`OnGUI`)**. UGUI/`AddComponent`/`Canvas` composition is DEAD on this
build. Do not re-add UGUI code; the full reasoning is in `AI-CONTEXT.md`.

## Repos

| Part | Repo | Visibility |
|------|------|------------|
| Mod (THIS repo) | `https://github.com/DALI951/xp-display-mod` | public |
| Game interop reference dlls (private) | `https://github.com/DALI951/supermarket-sim-interop` | private |

## Where the code lives (this machine)

- Source + build: `F:\mods\supermarket\xp\XPDisplayMod\`
  - `XPDisplayBehaviour.cs` (24,715B) — plugin behaviour, config `ConfigEntry`s, OnGUI
  - `XpHudRenderer.cs` (8,161B) — IMGUI HUD renderer (bar, floaters, popup, stats)
  - `ModsCfgPanel.cs` (854B) — the settings-panel stub (IMGUI, `Open/Close/Render`)
  - `XPDisplayMod.cs` (4,090B) — plugin entry
  - `XPDisplayMod.csproj` (5,109B)
- Built DLL (43,008B, proven build 21:53:42): `bin\Release\XPDisplayMod.dll`
- Deploy target: `C:\SteamUnlocked\Supermarket.Sim.v1.6.0.ALL.DLC\...\BepInEx\plugins\XPDisplayMod.dll`
- Decompiled/interop refs used to compile: `...\BepInEx\interop\` (UnityEngine.*, Il2CppSystem.*, Assembly-CSharp.dll etc.)

## Build command (proven, ASCII-safe)

```powershell
$msb = "$env:windir\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
$ros = "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn"
& $msb F:\mods\supermarket\xp\XPDisplayMod\XPDisplayMod.csproj /t:Build /p:Configuration=Release /v:q /nologo "/p:CscToolPath=$ros" /p:CscToolExe=csc.exe
```

## Wrong-prefix note

This machine's GitHub CLI uses the repo owner `DALI951` (the `DALI951/…` URLs above are
authoritative). The memory files may reference an older DALI951/DALI951 pair — trust the
URLs in this README.

## Next work item (see AI-CONTEXT.md for the full backlog)

Finish the **config panel** (`ModsCfgPanel.Render()`) so the "XP DISPLAY" tab opens a
**native-looking settings sub-screen** (matching the game's dark-#222 / gold-#FFDA60
look) with real toggles: ShowBar, ShowPopup, ShowFloaters, ShowStats, Scale, colors, tab
toggle key — each wired to the existing `XPDisplayMod.*` `ConfigEntry`s. The engine and
HUD are DONE and user-approved; the panel is the remaining feature.
