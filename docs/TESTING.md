# Verification

Date: 2026-10-21. Installed game: Slay the Spire 2 v0.107.1, .NET 9, Godot 4.5.1.

- Release build: passes with zero warnings and errors.
- Pure calculation checks: 11 pass. Covers duplicate and next-hand probabilities (including five draws from five cards at 100%), multi-target HP damage, generated versus used block, FIFO and expiry, effective healing, HP loss/self damage, combat/run aggregation, replay replacement, persistence and unknown sources.
- Harmony patch checks: 78 methods patch successfully against the installed game assembly in an isolated .NET 9 process. This includes direct events and provider contexts for block/healing from powers, relics, potions and orbs.
- Actual game observer checks: 8 pass with two simulated players in an isolated process. Executes the game's real block, HP-loss, healing and combat-history methods. Confirms exact damage blocked, source-card block credit, effective ally healing, overkill exclusion and separate player totals.
- Steam startup: the game detects, initializes and attaches the informational mod.
- Native UI fixtures: all five tabs render with the game's fonts, power icons and character/card art. Scope switching, Back, Escape/cancel through the game's hotkey manager, reopening, stable controls across live refreshes and native card inspection pass. The next-hand badge displays 100% for a five-card draw from five remaining cards and stays inside the card. Captured previews were visually inspected. The top-bar entry uses the native container rather than fixed coordinates; its container layout is checked separately from a live run.
- One-time progression restoration: backed up both profiles before changing files. Imported the existing progress and 46 histories into modded profile 1 through the game's local/Steam Cloud save API, verifying cloud contents. A fresh Steam launch without import code loads the original 16-unlock count. Original progression and the existing modded active-run file retain their exact backup hashes; all 46 imported histories match their sources. The temporary import helper was removed, and no save import code is shipped.

The native UI fixture is included only when building with `-p:UiSmoke=true`; it is excluded from the release DLL. It uses sample statistics and temporary card objects, starts no run and saves no run. Its preview is not evidence of a live multiplayer match.

Remaining manual checks are in `TODO.md`: a full combat with normal controls, and a two-client multiplayer session including support effects, simultaneous actions, transitions and reloads. The first Backstage setup check also leaves the project icon task open.
