# To-do

## Build

- [x] **Track combat and run statistics** — Show each player's damage, damage per turn and damage per second in Current Combat and Overall Run views. Record block generated, damage actually absorbed by block, actual HP lost, effective healing given to other players and damage from area attacks. Track resolved results without changing gameplay. (2026-10-21)
- [x] **Add card statistics** — Clicking a card opens its combat and run statistics: uses, actual damage, AoE damage, block generated, effective block where attribution is known, healing and other observed results. Preserve normal card interactions. (2026-10-21)
- [x] **Show draw probabilities** — In the draw-pile view, show each card type's chance on the next random draw, counting remaining duplicates together. Do not reveal hidden order or consume randomness. (2026-10-21)
- [x] **Keep the interface compact** — Use a small damage panel and an on-demand menu for detailed statistics. Keep every label in English. (2026-10-21)
- [x] **Build an installation package** — Compile against the installed game and provide a repeatable build, test and installation workflow. Keep game binaries out of Git. (2026-10-21)

## Checks

- [ ] **Check the interface in game** — Open a combat, switch combat/run views, open card details and check draw-pile odds. Verify normal controls and card clicks still work. (2026-10-21)
- [ ] **Check multiplayer** — Compare two players' damage, generated/effective block, HP loss, healing and AoE results against observed actions. Check whether other players need the mod. (2026-10-21)

## Setup

- [x] **Register in Backstage** — Link project instructions, register this folder, run its setup check and rebuild the Backstage snapshot. (2026-10-21)
- [ ] **Upload the Git repository** — Create the repository using the owner's identity, exclude local/game files and push the checked source to GitHub. (2026-10-21)
- [ ] **Add a project icon** — Connect suitable existing app art and verify it on Backstage's project card and sidebar in both themes. (2026-10-21)
