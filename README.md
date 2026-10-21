# Spire Stats

An English informational mod for **Slay the Spire 2** on Windows.
Built against the locally installed game version **v0.107.1** (.NET 9 / Godot 4.5.1).

## Features

- A small movable damage panel with **Current Combat** and **Overall Run** views.
- Each player's actual damage, damage per second and damage per turn.
- A **Stats** button for detailed player statistics and a separate card list.
- Block generated, damage absorbed by block, HP lost and self-inflicted HP loss.
- Effective healing given, healing given to allies, and healing received.
- Damage from attacks targeting all opponents, including damage across targets.
- Per-card uses, draws, damage, AoE damage, generated/used block, healing, energy, stars and power stacks.
- A combat/run comparison in the normal card inspection screen. Click a card in the deck or a pile to inspect it; normal card-playing controls are preserved.
- **Next draw** labels on cards in the draw-pile view. Two copies among ten remaining cards display **20%**, rather than 10% per copy.
- Local run statistics, including previous combats, saved separately from the game's saves.

Click **Stats** to open or close details. The **Player** and **Cards** buttons choose the view.
The small **−** button collapses the player rows. Drag **Spire Stats** to move the meter.

## Install

Close the game. Extract `STSOverlay.zip` into the game's `mods` folder:

```text
Slay the Spire 2/
  mods/
    STSOverlay/
      STSOverlay.dll
      STSOverlay.json
```

Start the game and enable Spire Stats if its mod menu requests it.
To uninstall, close the game and remove only `mods/STSOverlay`.
This mod does not copy, modify, link or delete game saves.
The game itself uses a separate modded profile when mods are enabled.

## Multiplayer

The mod observes each client's existing replicated combat events. It adds no network protocol or gameplay actions.
Its manifest sets `affects_gameplay` to `false`. In the inspected v0.107.1 game code, these mods are excluded from the gameplay-mod compatibility list, so teammates should not need it merely to join. Each person who wants the interface needs a local installation.
An actual two-client multiplayer session remains a required verification; see [TODO.md](TODO.md).

## What the numbers mean

- **Damage** is actual enemy HP removed, excluding blocked damage and overkill.
- **AoE damage** is the portion of that damage from attacks targeting all opponents. It is included in Damage, not added again.
- **Block generated** is the actual increase in block, after modifiers and caps.
- **Damage blocked** is the game's resolved blocked damage on that player.
- **Block used (FIFO)** credits the oldest remaining block first to its source player and card. This is an attribution convention: the game merges block into one pool, so there is no uniquely correct per-card split. Expired or removed block earns no credit.
- **Healing** is HP actually restored, excluding overhealing. **Healing given to allies** excludes self-healing. Unidentified healing is counted as received healing without inventing a healer.
- **Uses** includes automatic plays and replays. Energy and stars are counted once for a paid replay series.
- Card statistics group a player's copies and upgrade versions by card type. They are observed results, not predicted future effects or causal credit for every later effect of a power.
- Shared effects without a unique known owner remain unattributed. The meter shows their damage separately rather than assigning it to a teammate. Poison with one known contributor is credited to that source; ambiguous shared poison is not divided arbitrarily.
- Doom executions count the HP actually removed by the execution, using a known triggering card or unambiguous Doom contributor. Applying Doom stacks alone does not count as damage.
- **DPS** uses elapsed combat time, including thinking time and animations, and excludes time outside combat and explicit pauses. Overall DPS divides all tracked damage by all tracked combat time. DPT uses combat rounds.
- **Next draw** means one uniformly random draw from the remaining draw pile. It counts copies by card type, including upgrades. Known top-deck placement, ordered draws, or replacement effects can override that assumption. The mod never reads hidden order or advances RNG.
- Statistics begin when the mod observes a fight. Older fights from before installation cannot be reconstructed. Replaying a fight replaces its earlier contribution to prevent double counting. Only this mod's own JSON files are written, under the game's user-data folder in `spire-stats/`.

## Build and check

Install .NET SDK 10. The mod references `sts2.dll`, `GodotSharp.dll`, and `0Harmony.dll` from your own game installation. No game assemblies are distributed or committed.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-hooks.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install.ps1
```

The build and installer discover Steam libraries. For a custom installation, pass `-GameDir "D:\path\to\Slay the Spire 2"`, or set `STS2_DIR`.
The build produces `dist/STSOverlay.zip`. It does not install automatically.

The calculation checks cover duplicates, multi-target damage, actual versus generated block, expired block, effective healing, self damage, run aggregation, replay replacement, persistence and unknown sources. Hook checks apply the real Harmony patches to the installed game assembly in an isolated .NET 9 process. They do not launch the game or prove UI/multiplayer behavior. In-game and multiplayer verification are tracked separately.

## Project hub

Registered in the owner's local Backstage hub. `backstage.json` contains commands and checked progress; `TODO.md` records remaining work.
