# Home Mover — Full Move Plan

**Status:** Design / Pre-implementation  
**Date:** April 2026  
**Scope:** Everything a player selects should be moved intelligently — roofs, conduits, wall-mounted items, furniture, floors — in the correct order, with automatic obstruction handling.

---

## 1. Vision

The player drag-selects an area of their base. Everything inside — floors, conduits, walls, doors, switches, lamps, furniture, shelves — gets picked up and placed at the destination as a group. The move is self-contained and smart:

- The correct placement order is enforced so walls exist before the lamp goes on them.
- Obstructions at the destination are handled automatically (plants cut, rocks mined, player structures minified or warned about).
- Roofs are tracked so they don't collapse during transit and are rebuilt at the destination.
- Conduits and hidden infrastructure are laid down before the buildings that depend on them.
- Everything is safe across save/load.

---

## 2. Current State (Baseline)

### What Works
- **Building reinstall** — `Minifiable` buildings get `PlaceBlueprintForReinstall` / `PlaceBlueprintForInstall`.
- **Floor copying** — layerable terrain blueprints are queued at the destination.
- **Roof tracking** — `NoRoof` area set during transit; `RoofGrid_SetRoof_Patch` clears it after roof is removed.
- **Smart dependency order** — `RequiresSupport()` puts wall-attachments and conduits after structures. Currently heuristic-based (name strings, PlaceWorker names).
- **Deadlock resolution** — `ResolveDeadLock()` adds `Uninstall` designation when circular blocking is detected.
- **Plant clearing** — `DesignatePlantsBlocking()` adds `CutPlant` / `HarvestPlant` when `PlaceWaitingBuildings` is rejected.
- **Cache hygiene** — `RoofUtility` and `DesignatorHomeMover` caches cleared on load/new game.

### What's Missing / Broken
1. **Selection is buildings-only** — `CanDesignateThing` only accepts `Building` + `Minifiable`. Conduits, floors (terrain), wall lights, switches, vents, and non-minifiable vanilla buildings are ignored.
2. **Obstruction handling is passive** — plants are designated, but rocks/mineable walls, player-owned non-minifiable structures blocking the destination, and player-owned minifiable structures at the destination are not handled.
3. **Placement order is heuristic** — `RequiresSupport()` uses string matching on def names and PlaceWorker type names. Unreliable for modded content.
4. **Roof rebuild is not queued** — the mod tracks which roof cells to *remove* but never queues a `BuildRoof` designation at the destination.
5. **Non-minifiable buildings** — buildings that aren't minifiable (even with Minify Everything) are silently skipped or cause errors. No toast/warning.
6. **Conduits are not selected** — underground conduits (which have `def.building.isEdifice = false` and hide under floors) are not in the selection at all.
7. **No selection preview** — the player has no visual confirmation of *what* will be moved before committing.

---

## 3. RimWorld API Reference

Key types and methods used in this plan:

| Symbol | Purpose |
|---|---|
| `Building.def.Minifiable` | True if can be uninstalled |
| `Building.def.holdsRoof` | True for walls, pillars, etc. |
| `Building.def.building.isEdifice` | False for conduits, floor-level items |
| `Building.def.building.isAttachment` | True for wall-mounted items |
| `Building.def.PlaceWorkers` | PlaceWorker instances; `PlaceWorker_OnWall`, `PlaceWorker_WallAttachment`, etc. |
| `TerrainDef.layerable` | True for player-built floors |
| `TerrainDef.blueprintDef` | Not null if a floor can be queued for construction |
| `GenConstruct.CanPlaceBlueprintAt` | Placement validation |
| `GenConstruct.PlaceBlueprintForReinstall` | Queue reinstall |
| `GenConstruct.PlaceBlueprintForInstall` | Queue minified item install |
| `GenConstruct.PlaceBlueprintForBuild` | Queue new construction (floors, roofs) |
| `GenConstruct.BlocksConstruction` | True if thing physically blocks a blueprint |
| `Map.areaManager.NoRoof[cell]` | Marks cells for roof removal |
| `Map.areaManager.BuildRoof[cell]` | Marks cells for roof construction |
| `Map.designationManager.AddDesignation` | Add cut/mine/harvest/uninstall |
| `RoofCollapseUtility.RoofMaxSupportDistance` | ~6.9 cells — safe roof support range |
| `ThingRequestGroup.MinifiedThing` | For finding minified versions after uninstall |
| `InstallBlueprintUtility.CancelBlueprintsFor` | Cancel pending reinstall blueprint |
| `JobDefOf.Mine` / `DesignationDefOf.Mine` | Rock/wall mining |
| `DesignationDefOf.Deconstruct` | Deconstruct player structure |
| `DesignationDefOf.Uninstall` | Uninstall minifiable player structure |
| `DesignationDefOf.CutPlant` / `HarvestPlant` | Plant clearing |
| `MinifyUtility.MakeMinified` | Minify a building into a MinifiedThing |

---

## 4. Placement Order Specification

The correct order, from first to last blueprint queued, is:

```
1. Underground conduits        (isEdifice=false, under floors, e.g. PowerConduit)
2. Floor tiles                 (TerrainDef, layerable)
3. Walls & support structures  (holdsRoof=true, isEdifice=true, no PlaceWorkers requiring other buildings)
4. Doors                       (thingClass inherits Building_Door)
5. Large furniture/buildings   (isEdifice=true, not holdsRoof, not attachment, not conduit)
6. Wall-mounted items          (isAttachment=true, or PlaceWorker requiring adjacency to wall/building)
7. Small furniture/items       (isEdifice=false, small, no special PlaceWorkers)
8. Roof designation            (BuildRoof area after structures are in place)
```

### Classification Logic (`PlacementTier`)

Replace the current string-matching `RequiresSupport()` with a proper enum-based tier classifier:

```csharp
enum PlacementTier
{
    Conduit     = 0,   // isEdifice=false AND is power/pipe infrastructure
    Floor       = 1,   // TerrainDef (handled separately, not a Building)
    Structure   = 2,   // holdsRoof=true, isEdifice=true
    Door        = 3,   // thingClass is Building_Door
    Furniture   = 4,   // default for normal buildings
    WallMount   = 5,   // isAttachment=true OR has PlaceWorker_OnWall / PlaceWorker_WallAttachment
    SmallItem   = 6,   // non-edifice, non-conduit, non-attachment
    Roof        = 7,   // virtual, handled by BuildRoof area
}
```

**Classification rules (in order, first match wins):**

1. `isEdifice = false` AND (`def.EverTransmitsPower` OR def has `CompPowerTrader` props OR category is `Power`/`Structure`/utility conduit by PlaceWorker) → `Conduit`
2. `holdsRoof = true` AND `isEdifice = true` → `Structure`
3. `thingClass` inherits `Building_Door` → `Door`
4. `isAttachment = true` OR any PlaceWorker is `PlaceWorker_OnWall` / name contains "Wall" / name contains "Attach" → `WallMount`
5. `isEdifice = false` AND not conduit → `SmallItem`
6. Default → `Furniture`

**Modded content:** Because this checks structural properties (not string names), it works for modded conduits, wall-mounted items, etc. without explicit entries.

---

## 5. Selection Phase Changes

### 5a. What Gets Selected

Expand `CanDesignateThing` to include:

| Category | Condition | Notes |
|---|---|---|
| Buildings (current) | `Minifiable` + player-owned | No change |
| Buildings (new) | Non-minifiable player-owned | Accepted but flagged — will warn during placement that these cannot be physically moved |
| Conduits | `def.building.isEdifice == false` AND `def.EverTransmitsPower` (or equivalent for pipes) | Hidden conduits under floors |

Floors (terrain) are already handled by the separate `_floorPattern` dictionary and are **not** designated as Things — that stays the same.

### 5b. Auto-include Conduits Under Selected Area

When the player finishes their drag selection (in `FinalizeDesignationSucceeded`), automatically scan every selected cell for conduits that are not already selected and add them. This mirrors how dragging already picks up all things in the cell.

```csharp
// In FinalizeDesignationSucceeded or at the end of the Select drag:
foreach (IntVec3 cell in _selectedCells)
    foreach (Thing t in cell.GetThingList(Map))
        if (IsConduit(t) && !DesignatedThings.Contains(t))
            DesignateThing(t);
```

### 5c. Selection Visual Feedback

During the ghost preview (Mode.Place), show an info overlay listing:
- Count of things that will move
- Any non-minifiable buildings (yellow warning)
- Obstruction count at current mouse position (updated every frame via `CanDesignateCell`)

This can be a small `ImmediateWindow` added to `DoExtraGuiControls` alongside the rotation buttons.

---

## 6. Obstruction Handling

When `CanPlaceBlueprintAt` returns rejected for a waiting thing (in `PlaceWaitingBuildings`), categorize the blocker and handle it:

### 6a. Plants → Already done
`DesignatePlantsBlocking()` exists and works. No change needed.

### 6b. Mineable rocks / walls
```csharp
if (blocker.def.mineable)
    map.designationManager.AddDesignation(new Designation(blocker, DesignationDefOf.Mine));
```

### 6c. Player-owned Minifiable structure
```csharp
if (blocker is Building b && b.Faction == Faction.OfPlayer && b.def.Minifiable)
    map.designationManager.AddDesignation(new Designation(blocker, DesignationDefOf.Uninstall));
```

### 6d. Player-owned Non-minifiable structure
Cannot be automatically moved. Show a one-time letter/message (using the throttled log to prevent spam):
```
"[Home Mover] {thing.LabelCap} at {pos} is blocking the move destination and cannot be minified. 
Please move or deconstruct it manually."
```
Use `HomeMoverMod.DebugLog` + `Messages.Message` with `MessageTypeDefOf.CautionInput`.

### 6e. Foreign / enemy structures
Same as non-minifiable warning.

### Implementation: `HandleObstructionsBlocking()`

Replace the raw `DesignatePlantsBlocking` call in `PlaceWaitingBuildings` with a unified:

```csharp
private static void HandleObstructionsAt(BuildableDef def, IntVec3 pos, Rot4 rot, Map map)
{
    foreach (IntVec3 cell in GenAdj.OccupiedRect(pos, rot, def.Size))
    {
        foreach (Thing blocker in cell.GetThingList(map).ToList())
        {
            if (blocker is Plant plant)                        → DesignatePlant(plant, map)
            else if (blocker.def.mineable)                    → Mine designation
            else if (blocker is Building b)
            {
                if (b.Faction == Faction.OfPlayer && b.def.Minifiable)
                    → Uninstall designation
                else
                    → Toast warning (throttled)
            }
        }
    }
}
```

---

## 7. Roof Handling (Full Cycle)

### Current state
- Source roof: `NoRoof` area marks cells for removal. `RoofGrid_SetRoof_Patch` detects the removal and calls `SetNoRoofFalse`.
- Destination roof: **Nothing.** Roofs at destination are never queued for construction.

### Required additions

**7a. At placement time (when `PlaceBlueprintForReinstall` succeeds):**
For each building that `holdsRoof`, compute the roof cells that building *would* support at the destination and mark them in `BuildRoof`:

```csharp
if (building.def.holdsRoof)
{
    foreach (IntVec3 roofCell in BuildingRoofFootprint(building, destCell, rot, map))
        map.areaManager.BuildRoof[roofCell] = true;
}
```

`BuildingRoofFootprint` does a flood fill from destCell limited to `RoofMaxSupportDistance`, returning cells that would be under the support range. Use the same logic as `ThingUtility.RoofInRange`.

**7b. Roof collapse check before uninstall:**
The existing `WorkGiver_ConstructDeliverResourcesToBlueprints_Patch` already blocks pawns from starting an uninstall job if the building is the sole roof supporter. This is correct — leave as-is. The check uses `RoofUtility.IsSupported`.

**7c. Thick roofs (mountain roofs):**
Add a user setting that controls behavior when thick roof is detected above selected supports:

- `allowThickRoofMoves = false` (default): block the move with a message.
- `allowThickRoofMoves = true`: allow the move, but skip all roof migration logic for thick-roof cells (do not remove/track/rebuild those cells).

This keeps the safe default while allowing advanced users to proceed when they explicitly accept roof risk.

Check during `FinalizeDesignationSucceeded`:
```csharp
bool hasThickRoof = selectedBuildings
    .Where(b => b.def.holdsRoof)
    .SelectMany(b => b.OccupiedRect())
    .Any(cell => cell.GetRoof(map) == RoofDefOf.RoofRockThick);

if (hasThickRoof && !Settings.allowThickRoofMoves)
{
    Messages.Message("Cannot move: overhead mountain roof detected.", ...);
    CancelEntireOperation();
    return;
}

if (hasThickRoof && Settings.allowThickRoofMoves)
{
    // Proceed with move, but ignore thick-roof migration bookkeeping.
}
```

---

## 8. New File / Class Structure

No new files are strictly required — changes fit into existing files. However, extracting the obstruction handler is clean:

### Modified files

| File | Change |
|---|---|
| `Source/Designator/DesignatorHomeMover.cs` | Major — placement order, obstruction handling, conduit auto-include, roof destination queuing, thick-roof check, non-minifiable warning |
| `Source/Core/HomeMoverSetting.cs` | Add setting: `handleObstructions` (mine/uninstall blockers automatically), `warnNonMinifiable`, `queueDestinationRoof` |
| `Source/Core/UIText.cs` | Add new string keys |
| `Source/Utilities/ThingUtility.cs` | Add `PlacementTier GetTier(Thing)` replacing `RequiresSupport` |

### New files

| File | Purpose |
|---|---|
| `Source/Utilities/PlacementOrder.cs` | `PlacementTier` enum + `GetTier(Thing)` classifier. Extracted from ThingUtility for clarity. |
| `Source/Utilities/ObstructionHandler.cs` | `HandleObstructionsAt(def, pos, rot, map)` — plant/mine/uninstall/warn logic. Extracted from DesignatorHomeMover. |

---

## 9. Settings

| Setting | Default | Description |
|---|---|---|
| `smartDependencyPlacement` | true | Already exists — now uses `PlacementTier` |
| `handleObstructions` | true | Auto-designate plants/rocks/structures blocking the destination |
| `autoMinifyBlockers` | true | Automatically add Uninstall designation to player buildings blocking destination |
| `warnNonMinifiable` | true | Show toast when a blocker cannot be minified |
| `queueDestinationRoof` | true | Queue BuildRoof at destination after structural buildings are placed |
| `allowThickRoofMoves` | false | When true, allow moves under thick roof and ignore thick-roof migration logic |
| `skipItemsWithErrors` | true | Already exists |
| `showSkippedItemsMessage` | true | Already exists |
| `copyFloorTypes` | true | Already exists |
| `enableDebugLogging` | false | Already exists |

---

## 10. Key Edge Cases and Decisions

### 10a. Non-minifiable buildings in the selection
- **Selected at source:** Flag them in `DesignatedThings` with a separate set `_nonMinifiable`. They receive the `HomeMover` designation but will be warned about during placement.
- **During placement:** Skip them from `PlaceBlueprintForReinstall`. Show per-item warning.
- Rationale: The player may want to move everything around them but leave the non-minifiable in place, or they may have Minify Everything installed only for some items.

### 10b. Conduit selection — partial conduit runs
If the player only selects part of a conduit network, the selected segment will be moved but the remaining network stays. This is intentional and correct — the player is responsible for reconnection. No special handling needed beyond including them in the ordered placement.

### 10c. Multi-map / map change during transit
`GameSaveComponent.LoadedGame()` and `StartedNewGame()` already clear caches. The `RemoveRoofModel` is saved via `Scribe` so `WaitingThings` survives save/load on the same map.

### 10d. Deadlock between conduit and wall
A conduit `A` wants to be placed under wall `B`, but wall `B` is waiting for conduit `A` to be placed first. This is the existing `ResolveDeadLock` problem, now extended to conduits. The resolution: conduits have `PlacementTier = 0` (lowest), so they are always placed before walls. The deadlock should not occur if the tier sort is correct. If it does, `ResolveDeadLock` catches it and adds `Uninstall`.

### 10e. Thick roofs
By default, mountain/thick roofs are blocked (see §7c). If `allowThickRoofMoves` is enabled, moves are allowed and thick-roof migration is intentionally ignored. Thin roofs (player-built, `RoofDefOf.RoofConstructed`) are fully supported.

### 10f. Modded conduit types (CE ammo boxes, pipes, etc.)
The `PlacementTier` classifier uses structural property checks, not def names:
- `isEdifice = false` catches hidden-floor items.
- `EverTransmitsPower` catches power conduits.
- For non-power conduits (LWM Deep Storage's underground cables, etc.), the `isEdifice = false` + `!holdsRoof` + `!isAttachment` path falls through to `SmallItem` which still gets placed before wall-mounted things.

### 10g. Items the player cannot move (enemy turrets, etc.)
`CanDesignateThing` already rejects non-player, non-claimable buildings. No change.

---

## 11. Implementation Sequence

Tasks in order (each should build cleanly before proceeding to the next):

1. **Extract `PlacementTier`** — new `PlacementOrder.cs`, replace `RequiresSupport()` with tier-based sort. Build + test conduit placement order.

2. **Expand `CanDesignateThing`** — accept conduits and non-minifiable buildings. Add `_nonMinifiable` set. Add conduit auto-include after drag. Build.

3. **Placement sort** — in `DesignateSingleCell` (Place mode), replace the two-list `structuresFirst/infrastructureSecond` with a full tier-sort over all `DesignatedThings`. Build.

4. **Unified obstruction handler** — new `ObstructionHandler.cs`. Replace `DesignatePlantsBlocking` call in `PlaceWaitingBuildings` with `HandleObstructionsAt`. Build.

5. **Roof destination queuing** — in `PlaceWaitingBuildings`, after successful `PlaceBlueprintForReinstall`, queue `BuildRoof` at destination. Build.

6. **Thick roof check + override** — in `FinalizeDesignationSucceeded`, scan for `RoofRockThick` and branch by `allowThickRoofMoves` (block by default, allow+ignore roof migration when enabled). Build.

7. **Non-minifiable warning** — in Place mode, show warning for things in `_nonMinifiable`. Build.

8. **New settings** — add the new settings to `HomeMoverSetting` and the settings UI. Build.

9. **UI overlay** — add the info window in `DoExtraGuiControls` showing move summary. Build.

10. **Language strings** — add all new `UIText` keys to English and Chinese Simplified keyed XMLs.

11. **Full test pass** — test with base game only, then with Minify Everything, then with a heavy mod list.

---

## 12. Out of Scope

These are explicitly **not** in this plan:

- **Undo** — RimWorld has no undo infrastructure.
- **Auto-path planning** (move things through a corridor one at a time) — too complex.
- **Replacing another mod's version of conduit handling** — we work alongside other mods, not replace them.
- **Mining overhead mountain rock** — still out of scope. If thick-roof override is enabled, the move can proceed but thick roof itself is not modified by this mod.

---

## 13. Mod Compatibility Notes

Compatibility mods in this section are optional. Home Mover must run correctly with none of them present.
Only Minify Everything is a hard dependency.

| Mod | Interaction | Required action |
|---|---|---|
| **Minify Everything** | Required for non-vanilla minifiable buildings. Selection expansion works because ME adds `Minifiable` to more defs — our `CanDesignateThing` picks them up automatically. | None |
| **Replace Stuff** | Also patches `CanPlaceBlueprintAt`. Both patches coexist because we use mode gating (`BlueprintMode`). ReplaceStuff's `CanPlaceBlueprintRotDoesntMatter` transpiler is additive and safe. | Optional synergy only; no required integration |
| **Smarter Construction** | Patches `WorkGiver_Scanner.GetPriority` and `HandleBlockingThingJob`. No overlap with our patches. | Optional synergy only; no required integration |
| **Smarter Deconstruction** | Patches `JobDriver_RemoveBuilding.MakeNewToils` to add roof checks before deconstruction. Our path uses `JobDriver_Uninstall`, so behavior should remain independent. | Optional synergy only; verify no accidental coupling |
| **TD Enhancement Pack** | `MakeWayForBlueprint` and `HandleAllBlockingThings` improve blocker clearing throughput. | Optional synergy only; no required integration |

---

## 14. Known Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Conduit auto-include misidentifies modded items | Medium | `PlacementTier` classifier is conservative; misclassified items fall to `SmallItem` tier which is still correct |
| `BuildRoof` area causes roof to be built where player doesn't want it | Low | Only mark cells that were roofed at source AND are within support range of destination structure |
| Thick roof handling surprises users | Medium | Keep `allowThickRoofMoves=false` by default and show explicit warning when override is on |
| `ResolveDeadLock` breaks with conduits added to WaitingThings | Medium | Conduits have `PlacementTier=0` so they go first; deadlock only triggers if a conduit blocks a conduit, which is geometrically rare |
| Non-minifiable buildings in selection cause NRE in placement loop | High (current) | `_nonMinifiable` set is checked before `PlaceBlueprintForReinstall` is called |
