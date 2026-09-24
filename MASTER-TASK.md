# MASTER TASK — XPDisplayMod end-to-end (single source of truth)

{TASK: You are completing XPDisplayMod end-to-end, autonomously, across as many sessions
as needed. This file is the single source of truth for what's done, what's left, and how
to do each remaining piece. Read AI-CONTEXT.md and UI-CLONING-METHOD.md FIRST, fully,
before touching code - they explain the game's constraints and the cloning method this
whole plan depends on. Update THIS file's checklist as you complete each item, so the
next session (or you, later) knows exactly where things stand. Never mark something done
without having built + deployed + confirmed it works in-game.

=== WORKING RULES FOR EVERY SESSION ===

- Build with the Roslyn command in AI-CONTEXT.md. Confirm 0 error CS before ANY deploy.
- Deploy the DLL to BepInEx/plugins/, then request an in-game test before marking
  anything done. "Compiles" is not "works" - confirm visually/functionally every time.
- Never guess a sprite name, color, font, or component structure. If it's not already
  logged from a dump, add a DumpNode call for it, rebuild, capture the log, and only
  then write code against it. This project has been burned by guessed values before
  (the original #FFDA60 gold assumption was wrong - the real accent is
  0.192,0.384,0.6,1 blue, confirmed by actual dump data).
- Follow UI-CLONING-METHOD.md for anything visual. No IMGUI for native-looking elements.
  No generic AddComponent<T>(). Clone real game UI, don't hand-build it.
- One task at a time. Don't start task N+1 until task N is built, deployed, and
  confirmed working. If a task reveals a blocker, stop, report it, don't work around it
  silently.
- Keep every change scoped to the files that task actually needs. Don't refactor
  unrelated code. Don't rename existing public members (IsOpen/Open/Close/Toggle/Render
  on ModsCfgPanel must stay stable - other code depends on that exact shape).
- After finishing each task below, update the checklist status in this file (DONE /
  IN PROGRESS / BLOCKED + why) and commit + push, so state survives a session boundary.

=== CURRENT STATE (update this section as you go) ===

DONE:
  - HUD (bar, floaters, popup, stats) - proven IMGUI, working
  - In-game settings-screen open-detection - fixed, confirmed working (gate on
    whichever SettingsMenuManager instance has an active "Menu" child, checked fresh
    every tick, no caching)
  - Tab injection into the in-game PC-layout settings strip - confirmed working, tab
    visible and clickable in-game
  - ModsCfgPanel IMGUI skeleton (toggles/sliders/color rows/close) - functional but
    NOT native-looking, this is what Task 1 below replaces

IN PROGRESS / NEXT:
  - Task 1: Native UGUI config panel (see below)
  - Task 2: Native XP bar placement (see below)
  - Task 3: Native color pickers (deferred scope from Task 1)
  - Task 4: Final pass / cleanup

=== TASK 1: NATIVE UGUI CONFIG PANEL ===

Goal: replace the IMGUI ModsCfgPanel visuals with real cloned UGUI, per
UI-CLONING-METHOD.md. Scope: ModsCfgPanel.cs only, unless a dump requires adding one
more DumpNode call to XPDisplayBehaviour.cs (that's fine, keep it minimal).

Steps:
  1. If not already captured: dump "Window BG" and "Taskbar" (or whatever the panel's
     actual outer-background node is named - confirm via the existing settings-tree
     dump) for sprite/type/color. This is the panel background source.
  2. Clone the panel background from that node.
  3. Clone "Invert X Axis Toggle" (or equivalent) once per row: ShowBar, ShowPopup,
     ShowFloaters, ShowStats, RewardEnabled. Wire each to its matching
     XPDisplayMod.*.Value ConfigEntry both ways (read on create, write on change).
  4. Clone "FOV Setting Slider" for UI Scale (remap 0.5-2.0) and Reward Per Level
     (remap 0-500). Same two-way wiring.
  5. Clone "Back Button" for Close, retarget onClick to ModsCfgPanel.Close(), relabel
     its TMP text to "Close".
  6. Every label added or retargeted uses TMPro.TextMeshProUGUI, values cloned from
     an existing "Text (TMP)" node, not guessed.
  7. Leave color rows (Background/Accent/Text) as IMGUI for now - that's Task 3.
  8. Build panel hierarchy once on Open(), Destroy() the root on Close(), guard against
     leaks on repeated open/close.
  9. Build, deploy, test: open settings, click tab, confirm native look, toggle each
     setting and confirm the HUD reacts, close and confirm no leaked GameObjects.

Report back exactly what still looks non-native after this (spacing, missing hover
states, wrong row order, etc.) - that becomes follow-up polish, not a blocker for
marking this task done.

=== TASK 2: NATIVE XP BAR PLACEMENT ===

Goal: anchor the mod's XP bar next to the game's own native XP element instead of a
fixed top-right offset.

Confirmed anchor: `Store Point Slider` at path
`---UI---/Ingame Canvas/Store Point Slider` - this is the game's own native XP/level
UI element, confirmed present during live gameplay via the native-XP scan.

Steps:
  1. Add a one-time lookup (same one-shot-gated pattern as other dumps) that finds
     Store Point Slider by path/name via FindObjectsOfType or Transform traversal from
     "Ingame Canvas", and dumps its RectTransform (anchoredPosition, sizeDelta, anchors,
     pivot) plus its parent Canvas's render mode/scale settings. Don't guess screen-space
     vs canvas-space - read it from the dump.
  2. Using that data, compute where XpHudRenderer's panel should sit so it appears
     directly adjacent to (not overlapping) Store Point Slider - above, below, or beside
     it, whichever reads cleanest given its actual size/position. Pick based on the
     dumped Rect, not assumption.
  3. This can either be: (a) a new ConfigEntry-driven "anchor to native XP bar" toggle
     that computes OffsetX/OffsetY dynamically each frame from Store Point Slider's
     live screen position (handles resolution changes automatically), or (b) a one-time
     computed default for OffsetX/OffsetY if the native bar's position is static. Prefer
     (a) if Store Point Slider could move (e.g. different UI states) - check the dump
     data / test in a couple of game states before deciding.
  4. Keep the existing drag-to-reposition behavior (XpHudRenderer.HandleDrag) working -
     if the player drags the mod's bar, that should override the auto-anchor until they
     reset it, OR simply keep it as the DEFAULT offset that Dali can still drag away
     from - implement whichever is less code and confirm with a quick test.
  5. Build, deploy, test: confirm the XP bar visually sits next to the native XP element
     in actual gameplay, at default settings, without manual offset tweaking.

=== TASK 3: NATIVE COLOR PICKERS (lower priority, do after Task 1 + 2 work) ===

Goal: replace the IMGUI HorizontalSlider RGB triples with whatever native color-picker
UI the game itself uses - confirmed present in the dump ("Color Panel" / "Text Color
Picker" / "GridColorPicker" nodes exist in the graphics settings tab).

Steps:
  1. Dump "Color Panel", "Text Color Picker", "GridColorPicker" fully (sprite/TMP/
     Toggle/Slider/children detail) - these weren't captured in prior passes.
  2. Determine if it's a feasible clone (does it work standalone, reparented under our
     panel, without needing the graphics-tab-specific context it normally lives in?) or
     if it's too tightly coupled to its original tab's logic to safely extract.
  3. If feasible: clone it per UI-CLONING-METHOD.md, wire its output color to
     XPDisplayMod.BgColor/AccentColor/TextColor ConfigEntry.Value (same string
     "r,g,b,a" format via ParseColor/ToString as the current IMGUI version does).
  4. If NOT feasible (too coupled, crashes on extraction, etc.): report why, and fall
     back to keeping the current IMGUI RGB sliders permanently - that's an acceptable
     outcome, don't force a broken clone. Note this decision in this file's checklist.

=== TASK 4: FINAL PASS ===

Once Tasks 1-3 are done (or Task 3 is explicitly deferred with reasoning logged):

  1. Full playtest: fresh game launch -> main menu settings (confirm still works,
     wasn't regressed) -> load into gameplay -> in-game settings -> XP DISPLAY tab ->
     every toggle/slider -> close -> earn XP in-game -> confirm bar/floaters/popup/stats
     all still work -> level up -> confirm reward + popup -> confirm native XP bar
     placement looks right.
  2. Check BepInEx log for any warnings/errors across that full playtest, not just
     "did it crash" - silent failures (caught exceptions that skip a feature) count as
     bugs.
  3. Update README.md's "Files" and feature list to match final reality (remove any
     "planned"/"in progress" language that's now done).
  4. Clean up AI-CONTEXT.md's "CURRENT STATE" and "TODO" sections to reflect the
     finished mod, so a future session (a real feature request, a game update breaking
     something) starts from an accurate picture, not stale backlog.
  5. Final commit + push.

=== IF YOU GET STUCK ===

Report the blocker precisely: what you tried, what you expected, what actually
happened (exact log lines/errors), and what you need (usually: one more specific dump).
Don't silently fall back to IMGUI or a hand-built workaround for something
UI-CLONING-METHOD.md says should be cloned - that's exactly the pattern that produced
the non-native look this whole effort exists to fix. A confirmed blocker with a clear
report is a fine outcome. A silent regression to hand-drawn UI is not. }

---

## Session status (2026-09-24)

- Task 1 IN PROGRESS: v3 UGUI clone built + deployed (0 CS errors, DLL 43008 B). Panel
  uses real frame sprite Frame_SpeechBubble01 (Sliced, ppu=3, color 0,0,0,0) + cloned
  Invert X Axis Toggle rows + FOV Setting Slider rows + Back Button clone relabeled
  Close, TMP labels cloned. Dali's first look: "a lot of UI problems" - follow-up
  analysis in progress (log + screenshot capture). Details pending before next fix.
- Part-1 dump data (captured): Window BG = sprite Frame_SpeechBubble01, Sliced,
  color 0,0,0,0, ppu=3, anchor (0.03,0.03)->(0.98,0.98); Taskbar = sprite null,
  Sliced, color 0.495,0.841,1,0.392, ppu=4, anchor (0.00,0.83)->(1.00,0.90).