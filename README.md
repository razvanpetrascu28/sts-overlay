# Spire Stats

An English informational mod for **Slay the Spire 2** on Windows. Built against game **v0.107.1**.

## Use it

Click **Stats** beside the map button in the run's top bar. It opens a single statistics screen using the game's fonts, card art and power icons.

- **Overview** compares total damage, damage per round, damage blocked and ally healing.
- **Offense** shows damage, AoE damage, damage per round and card plays.
- **Defense** separates generated block, actual damage blocked, HP lost and self damage.
- **Support** shows effective healing given, ally healing, healing received and power stacks.
- **Cards** lists a player's uses, damage, block and healing. Click a card to inspect its combat/run results.

Choose **This Combat** or **Entire Run** once at the top. **Back** or **Escape** closes the screen. There is no floating damage panel, collapse button or per-second rate.

In the draw-pile view, **Next hand** is the probability of seeing at least one copy in the next hand-sized set of draws. Drawing five cards from five remaining cards displays **100%**. Two copies among ten cards with a five-card draw display **77.8%**. It uses the current hand-draw modifiers and assumes random draws; future restrictions or ordered cards can change the result. It never reads hidden order or consumes randomness.

## Install

Close the game. Extract `STSOverlay.zip` into its `mods` folder:

```text
Slay the Spire 2/mods/STSOverlay/
  STSOverlay.dll
  STSOverlay.json
```

Start through Steam and accept Load Mods if requested. To uninstall, close the game and remove only `mods/STSOverlay`.

### Existing progression

The game stores normal and modded profiles separately. Enabling a mod can therefore appear to reset progress even while the original files are still present. Importing existing unlocks/history is a separate one-time operation: close the game, back up both profiles and update both local and Steam Cloud storage. Copying local files alone is insufficient because the game's cloud sync can replace or delete them. This mod and its installer never import progression automatically, redirect saves, or create links between profiles. Future modded progress stays in the modded profile.

## Statistics definitions

- Damage is actual enemy HP removed, excluding blocked damage and overkill. AoE is included in that total.
- Damage per round divides damage by combat rounds; the run view uses the sum of rounds across tracked fights.
- Damage blocked is the game's actual absorbed damage on the player. Block generated is the actual increase after modifiers and caps.
- Per-card block used credits the oldest remaining block first (FIFO). This is a convention because the game merges block into one pool. It is explained in a tooltip.
- Healing counts HP restored, excluding overhealing. Ally healing excludes self-healing.
- Card results group copies and upgrade versions. Plays include replays; energy and stars are charged once per paid replay series. Inactive zero-value effects stay out of the card details.
- Shared effects without a unique owner remain unattributed. Single-source poison and resolved Doom execution HP are tracked; applying Doom stacks alone is not damage.
- Run statistics are stored in this mod's own `spire-stats/` JSON files. Replaying a fight replaces its previous contribution. Fights before the mod was installed cannot be reconstructed.

## Multiplayer

The manifest sets `affects_gameplay` to `false`. The inspected game excludes these mods from its gameplay-mod compatibility list, so teammates should not need it simply to join. Each person who wants the interface installs it locally. No custom network messages or gameplay actions are added. A live two-client match still needs verification.

## Build from source

Install .NET SDK 10 and keep the game installed locally. No game assemblies are distributed in the repository.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-hooks.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install.ps1
```

The scripts discover Steam libraries. Pass `-GameDir "D:\path\to\Slay the Spire 2"` or set `STS2_DIR` for a custom install. The build produces `dist/STSOverlay.zip` and does not install it automatically.

See `docs/TESTING.md` for verification, `TODO.md` for remaining checks and `docs/IDEAS.md` for optional suggestions. Project commands and progress are registered in the owner's local Backstage hub.
