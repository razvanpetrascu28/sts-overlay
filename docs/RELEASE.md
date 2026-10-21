# Spire Stats 0.2.0 preview

An informational Slay the Spire 2 mod with a Stats entry in the run's top bar and a tabbed statistics screen using the game's fonts and icons.

Replaces the floating meter, minus button and repeated headings with Overview, Offense, Defense, Support and Cards tabs. Uses damage per round instead of DPS. Back/Escape closes the screen; clicking a card opens its native inspection screen with compact combat/run results.

Draw-pile labels now show the probability of at least one copy in the next hand. Five draws from five remaining cards show 100%; two copies among ten cards with five draws show 77.8%.

Includes damage, AoE damage, generated block, actual damage blocked, HP lost, effective healing to allies, card uses/draws, energy, stars and power stacks. Per-card block consumption uses FIFO because the game merges block into one pool.

Extract `STSOverlay.zip` into the game's `mods` folder. Enable Spire Stats and accept the game's first-time Load Mods prompt if needed. Close the game before updating.

Built against Windows game v0.107.1. Calculation checks, real hook application, simulated two-player observer checks and native UI preview checks are recorded in `docs/TESTING.md`. Existing normal and modded progression are separate game profiles; the mod does not import or redirect saves automatically.

A full live multiplayer session remains unverified. Shared effects without a unique owner stay unattributed. See the README for definitions and limitations.
