# Verification

Date: 2026-10-21. Installed game: Slay the Spire 2 v0.107.1, .NET 9, Godot 4.5.1.

- Release build: passes with zero warnings and errors.
- Pure calculation checks: 10 pass. Covers duplicate probabilities, multi-target HP damage, generated versus used block, FIFO and expiry, effective healing, HP loss/self damage, combat/run aggregation, replay replacement, persistence and unknown sources.
- Harmony patch checks: 78 methods patch successfully against the installed game assembly in an isolated .NET 9 process. This includes direct events and provider contexts for block/healing from powers, relics, potions and orbs.
- Actual game observer checks: 8 pass with two simulated players in an isolated process. Executes the game's real block, HP-loss, healing and combat-history methods. Confirms exact damage blocked, source-card block credit, effective ally healing, overkill exclusion and separate player totals.
- Steam startup: the game detects, initializes and attaches the informational mod.
- Native UI fixtures: player and card menus render and open through their button callbacks. The duplicate draw label displays 20.0% for two copies among ten cards, stays inside the card, and the normal inspection screen shows combat/run results. Panel bounds and stable controls across live refreshes pass. Captured previews were visually inspected.

The native UI fixture is included only when building with `-p:UiSmoke=true`; it is excluded from the release DLL. It uses sample statistics and temporary card objects, starts no run and saves no run. Its preview is not evidence of a live multiplayer match.

Remaining manual checks are in `TODO.md`: a full combat with normal controls, and a two-client multiplayer session including support effects, simultaneous actions, transitions and reloads. The first Backstage setup check also leaves the project icon task open.
