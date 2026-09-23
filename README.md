# XP Display Mod — Supermarket Simulator (IL2CPP / BepInEx 6)

An XP / level HUD + configurable display panel for **Supermarket Simulator v1.6.0
(all DLC, SteamUnlocked IL2CPP build)**. Built with BepInEx 6 (IL2CPP) + BepInEx.Core
6, C# 7, and rendered entirely through **IMGUI** (see the hard rule below — UGUI/AddComponent
is dead in this IL2CPP build).

**Repo links**
- Mod (this repo): `https://github.com/DALI951/xp-display-mod`
- Game interop (references the mod compiles against, PRIVATE):
  `https://github.com/DALI951/supermarket-sim-xtream-IL2CPP-interop`

## What it does
- Gold XP bar (top center) + XP-to-level fill, scale + offset configurable.
- **Level-up popup** (big gold banner centre).
- **+XP / -XP floaters** on the HUD.
- **Session stats line** (XP this session, XP/hr, level-ups).
- A **native-looking settings-screen panel** (the "XP DISPLAY" tab that appears at the top
  of the game's own UI) that holds ALL the config toggles/sliders/colours, rendered by the
  SAME IMGUI pipeline as the HUD.
- Tab strip + gold "XP DISPLAY" tab-label (appears like an in-game feature).

## THE HARD RULE (why the code looks the way it does)
This game is **IL2CPP-stripped**. Generic `GameObject.AddComponent<T>()` throws a
`TypeInitializationException` at runtime (the stripped generic `MethodInfoStore...` class has
no body). Non-generic `AddComponent(System.Type)` wants `Il2CppSystem.Type` and won't take a
plain `System.Type`. A fresh UGUI `Canvas` cannot be composed either.

**Therefore: everything draws with Unity IMGUI (`OnGUI`)** — `GUI.DrawTexture`,
`GUI.Label`, `GUI.DrawTexture`(rounded texture built with `SetPixels`), builtin Arial font
via `Resources.GetBuiltinResource<Font>("Arial.ttf")` — the SAME proven pipeline as the HUD
bar, floaters, popup, stats and tab label. OnGUI draws AFTER every UGUI canvas, so the
settings panel appears on top of the game's settings window exactly like a native sub-screen.

## Build (proven command — use THIS, not plain MSBuild)
The framework `csc` cannot parse C# 7. Use the VS Build Tools **Roslyn** compiler:

```powershell
$msb = "$env:windir\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
$ros = "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn"
& $msb .\XPDisplayMod.csproj /t:Build /p:Configuration=Release /v:q /nologo `
  "/p:CscToolPath=$ros" /p:CscToolToolExe=csc.exe
```

The built DLL is `bin\Release\XPDisplayMod.dll`. Deploy to the game's
`BepInEx/plugins/XPDisplayMod.dll`.

## Files
- `XPDisplayMod.cs` — plugin entrypoint + BepInEx `ConfigFile` bindings (scale, offsets,
  colours as "r,g,b,a" strings, toggles, keys, reward config).
- `XPDisplayBehaviour.cs` — the Unity `BaseBehaviour`. OnGUI → HUD renderer +
  `ModsCfgPanel.Render()` when the config screen is open. Tab-click → `ModsCfgPanel.Open()`.
- `XpHudRenderer.cs` — HUD bar / floaters / popup / stats / tab label, all IMGUI.
- `XPDisplayBehaviour.cs` — XP session tracking + render control (see file).
- `ModsCfgPanel.cs` — the config-screen IMGUI class (`IsOpen` / `Open` / `Close` / `Render`).

**Priorities / natural next step (in-repo AI-CONTEXT.md has the full backlog).** The config
panel currently opens when you click the "XP DISPLAY" tab (or F2 toggle). Expand it to look
exactly like a native settings screen: title bar with gold accent, section labels, toggles
for each HUD part, scale slider, colour rows, config-entry-driven reward toggle. Reuse the
proven `GUI` primitives and the gold `#FFDA60` accent. ALWAYS build with the Roslyn command
above and confirm 0 `error CS` before deploying.

## License
GPL-3.0. This is DALI951's own code; the game binaries are NOT in this repo (they live in
the private interop repo).
