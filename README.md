# Home Mover

A RimWorld mod for moving minifiable buildings to a new location. Handles groups of buildings at once, with rotation support and smart placement ordering.

## Features

- Select multiple buildings and move them as a group
- Rotation support during placement
- Handles roof removal for buildings that support roofs
- **Smart dependency placement** — wall attachments (lights, ACs, vents, coolers) and power conduits are placed *after* the structures they attach to, preventing most "must be placed against a wall" errors
- **Skip-on-error mode** — items that can't be placed are skipped rather than aborting the entire move; a message lists what was skipped and why
- **Copy floor types** — queues construction of matching floor tiles at the destination

## Settings

All options are available under Mod Settings → Home Mover:

| Setting | Default | Description |
|---|---|---|
| Smart dependency placement | ON | Places wall attachments and conduits after structures |
| Copy floor types | ON | Queues floor construction at the destination to match the source |
| Skip items with errors | ON | Skips unplaceable items instead of cancelling the whole move |
| Show message for skipped items | ON | Shows a notification listing skipped items and reasons |
| Enable debug logging | OFF | Logs placement details to the dev console (requires dev mode) |

## Known Limitations

- **Floors and roofs** are terrain, not buildings — they cannot be selected or moved directly. The "Copy floor types" setting will queue construction of matching floors at the destination.
- **Wall attachments** (lights, ACs, vents) require their wall to exist at the destination. Smart dependency placement handles this automatically when walls are moved in the same group. If moving attachments to an already-built wall elsewhere, it works as normal.
- **Power conduits** — effects may not transfer properly after moving. Verify connections afterwards.
- **Items with complex multi-step dependencies** may need to be moved in stages.

## Requirements

- [Minify Everything](https://steamcommunity.com/sharedfiles/filedetails/?id=836912371) (to move non-minifiable buildings)

## Credits

- Original mod by NotTooShabbySoftware
- Current maintainer: Jellypowered
- Inspiration from [Multi-Reinstall](https://steamcommunity.com/sharedfiles/filedetails/?id=2048885052) by oelsart
- Chinese translation by 不久
