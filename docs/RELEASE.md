# Spire Stats 0.1.0 preview

An informational Slay the Spire 2 mod with a small damage meter, combat/run views, an on-demand statistics menu, card results and draw-pile probabilities.

Includes damage, AoE damage, generated block, actual damage blocked, HP lost, effective healing to allies, card uses/draws, energy, stars and power stacks. Per-card block consumption uses FIFO because the game merges block into one pool.

Extract `STSOverlay.zip` into the game's `mods` folder. Enable Spire Stats and accept the game's first-time Load Mods prompt if needed. Close the game before updating.

Built against Windows game v0.107.1. Calculation checks, real hook application, simulated two-player observer checks and native UI preview checks are recorded in `docs/TESTING.md`.

A full live multiplayer session remains unverified. Shared effects without a unique owner stay unattributed. See the README for definitions and limitations.
