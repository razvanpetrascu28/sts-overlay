# To-do

## Build

- [x] **Track combat and run statistics** — Show each player's total damage and damage per round in This Combat and Entire Run views. DPS was originally requested and replaced with a turn-based metric at the owner's request. Record block generated, damage actually absorbed by block, actual HP lost, effective healing given to other players and damage from area attacks. Track resolved results without changing gameplay. (2026-10-21)
- [x] **Add card statistics** — Clicking a card opens its combat and run statistics: uses, actual damage, AoE damage, block generated, effective block where attribution is known, healing and other observed results. Preserve normal card interactions. (2026-10-21)
- [x] **Show draw probabilities** — In the draw-pile view, show the chance of at least one copy in the next hand, counting remaining duplicates together. Replaces the original single-draw percentage. Do not reveal hidden order or consume randomness. (2026-10-21)
- [x] **Keep the interface compact** — Use a Stats entry in the native top bar and an on-demand tabbed screen. The original floating meter was replaced at the owner's request. Keep every label in English. (2026-10-21)
- [x] **Build an installation package** — Compile against the installed game and provide a repeatable build, test and installation workflow. Keep game binaries out of Git. (2026-10-21)

## Checks

- [x] **Refine the native statistics screen** — Replaced the floating panel with a top-bar entry and five tabs using native fonts/icons. Removed DPS, duplicate scope labels, the no-op title and minus button. Grouped offense, defense, support and cards; explanations use tooltips. Back, Escape and card inspection pass the native fixture; full combat controls remain in the separate live check below. (2026-10-21)
- [x] **Correct next-hand probability** — Show at-least-one-copy chance over the next hand-sized draw, using the native draw-modifier API. Drawing five cards from a five-card pile shows 100% in the game fixture. Duplicate/partial-hand calculations and label bounds pass. Modifier interactions remain part of the full combat check. (2026-10-21)
- [x] **Restore existing progression** — Backed up both profiles. Imported progression and 46 histories into local and Steam Cloud modded storage; verified 16 unlocks survive a fresh launch. Original progression, modded active run and imported histories retain their source hashes. No import helper is shipped. (2026-10-21)

- [ ] **Check the interface in game** — Open a combat, switch combat/run views, open card details and check draw-pile odds. Verify normal controls and card clicks still work. (2026-10-21)
- [ ] **Check multiplayer** — Compare two players' damage, generated/effective block, HP loss, healing and AoE results against observed actions. Check whether other players need the mod. (2026-10-21)

## Setup

- [x] **Register in Backstage** — Link project instructions, register this folder, run its setup check and rebuild the Backstage snapshot. (2026-10-21)
- [x] **Upload the Git repository** — Create the repository using the owner's identity, exclude local/game files and push the checked source to GitHub. (2026-10-21)
- [ ] **Add a project icon** — Connect suitable existing app art and verify it on Backstage's project card and sidebar in both themes. (2026-10-21)
