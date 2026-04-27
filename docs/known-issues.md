# Known Issues

Issues identified during the April 2026 audit that were **not fixed** because they would change runtime behaviour. Each one needs a deliberate decision before touching.

---

## ~~1. Six Harmony Patches Are Never Applied~~ ✅ Fixed (April 2026)

All six classes were converted to use `[HarmonyPatch]` attributes so `PatchAll()` in `HomeMover_Startup` picks them up automatically. The dead `[StaticConstructorOnStartup]` constructors and all empty `harmony.Patch()` stubs were removed.

Two multi-target files were split into separate patch classes:
- `Blueprint_Destroy_Patch` → `Blueprint_Destroy_Patch` + `Blueprint_Install_TryReplace_Patch`
- `Designator_Patch` → `Designator_CancelThing_Patch` + `Designator_CancelSingleCell_Patch`

---

## ~~2. `RoofUtility._supportedRoof` Cache Is Never Cleared~~ ✅ Fixed (April 2026)

`RoofUtility.ClearCache()` was added and is now called from three `GameSaveComponent` lifecycle hooks:
- `ExposeData()` on `LoadSaveMode.LoadingVars` — clears before reference resolution during a load
- `LoadedGame()` — clears after a full game load (belt-and-suspenders)
- `StartedNewGame()` — clears when starting a fresh colony

Inspired by **SmarterConstruction**'s pattern of using lifecycle hooks (rather than TTL expiry) to invalidate map-session caches.

---

## ~~3. Two Different Harmony IDs Are Used~~ ✅ Fixed (April 2026)

All Harmony instances now use `"Jellypowered.HomeMover"` — updated in `HarmonyUtility.cs`, `RoofGrid_SetRoof_Patch.cs`, and `WorkGiver_ConstructDeliverResourcesToBlueprints_Patch.cs`.

---

## ~~4. Plants Blocking Placement Are Never Designated for Cutting~~ ✅ Fixed (April 2026)

`DesignatorHomeMover.PlaceWaitingBuildings` now has an `else` branch: when `CanPlaceBlueprintAt` returns rejected, `DesignatePlantsBlocking()` iterates the full building footprint (`GenAdj.OccupiedRect`) and adds `CutPlant` or `HarvestPlant` designations for any blocking plants (mirroring vanilla's `PlaceBlueprintForBuild` logic). Existing designations are not duplicated. Pawns with the Plants work type will then automatically clear the way, and the next `PlaceWaitingBuildings` tick will succeed.

Inspired by **TDEnhance**'s `MakeWayForBlueprint` module, which uses the same `HarvestableNow` check to decide between harvest and cut designations.
