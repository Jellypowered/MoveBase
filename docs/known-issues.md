# Known Issues

Issues identified during the April 2026 audit that were **not fixed** because they would change runtime behaviour. Each one needs a deliberate decision before touching.

---

## 1. Six Harmony Patches Are Never Applied

**Severity:** High — these patches are silently not running at all.

The following classes have `[StaticConstructorOnStartup]` and contain Postfix/Transpiler methods, but their static constructors never call `harmony.Patch()`. The methods exist but are never hooked into the game.

| File | Intended target | Method type |
|---|---|---|
| `Blueprint_Destroy_Patch.cs` | `ThingWithComps.Destroy` + `Blueprint_Install.TryReplaceWithSolidThing` | Postfix |
| `Designation_Notify_Removing_Patch.cs` | `Designation.Notify_Removing` | Postfix |
| `Designator_Deselect_Patch.cs` | `DesignatorManager.Deselect` | Prefix |
| `Designator_DesignateThing_Patch.cs` (class `Designator_Patch`) | `Designator_Cancel.DesignateThing` + `DesignateSingleCell` | Prefix |
| `JobDriver_Uninstall_FinishedRemoving_Patch.cs` | `JobDriver_Uninstall.FinishedRemoving` | Postfix |
| `GenConstruct_CanPlaceBlueprintAt_Patch.cs` | `GenConstruct.CanPlaceBlueprintAt` | Transpiler |

### Most critical
`GenConstruct_CanPlaceBlueprintAt_Patch` — without this transpiler, players **cannot** place a building on a tile occupied by another building that is also designated for moving. The whole "move building to occupied tile" use-case silently fails.

### Fix
Each class needs its static constructor to call `harmony.Patch()`, or be converted to use `[HarmonyPatch]` attributes (which `HarmonyUtility.MoveBase_Startup.PatchAll()` would then pick up automatically).

---

## 2. `RoofUtility._supportedRoof` Cache Is Never Cleared

**Severity:** Medium — stale results after map change or load.

**File:** `Source/RoofUtility.cs`

`_supportedRoof` is a `static Dictionary<Building, bool>`. It accumulates entries from the current map session and is never cleared. After a game load or map change, the dictionary can hold `Building` references pointing to destroyed or unloaded objects. A stale `true` entry means "this building supports a roof" for a building that no longer exists, which could cause the mod to incorrectly block a reinstall job.

### Fix
Clear the dictionary in `GameSaveComponent.ExposeData()` when `Scribe.mode == LoadSaveMode.LoadingVars`, or hook `GameComponent.StartedNewGame` / `GameComponent.LoadedGame`.

---

## 3. Two Different Harmony IDs Are Used

**Severity:** Low — cosmetic / tooling only.

`HarmonyUtility.MoveBase_Startup` creates `new Harmony("NotooShabby.MoveBase")`.  
`RoofGrid_SetRoof_Patch` and `WorkGiver_ConstructDeliverResourcesToBlueprints_Patch` each create `new Harmony("com.movebase.harmony")`.

Functionality is unaffected, but Harmony's patch-listing tools (e.g. `harmony.GetPatchedMethods()`) will only show patches belonging to the queried ID, making debugging harder.

### Fix
Pick one ID (recommend `"NotooShabby.MoveBase"` to match the author) and use it everywhere.

---

## 4. Plants Blocking Placement Are Never Designated for Cutting

**Severity:** Medium — causes permanent stuck state with no user feedback.

**Confirmed via:** In-game test (April 2026) — a tree in the target cell caused the mod to loop indefinitely.

### What happens
`PlaceWaitingBuildings` (called every ~6 ticks by `GameSaveComponent`) checks `GenConstruct.CanPlaceBlueprintAt` for each waiting building. When a plant is in the target cell, this returns rejected and the building stays in `WaitingThings` forever. Every pawn work evaluation also re-runs `WorkGiver_ConstructDeliverResourcesToBlueprints_Patch.Postfix`, producing continuous log spam.

### Why vanilla doesn't have this problem
For normal build blueprints, `GenConstruct.PlaceBlueprintForBuild` designates blocking plants for harvest/cut automatically as part of blueprint placement. The reinstall path (`PlaceBlueprintForReinstall`) does the same — but only once the mod successfully reaches that call. The mod's pre-check with `CanPlaceBlueprintAt` gates that call, so the designation never happens.

### Fix
In `PlaceWaitingBuildings`, when `CanPlaceBlueprintAt` returns rejected, inspect `deltaCell.GetThingList(map)` for `Plant` instances. For harvestable plants add `Designation_HarvestPlant`; for all others add `Designation_CutPlant`. This mirrors what vanilla does during normal blueprint placement and unblocks the stuck building automatically.

Note: there is no existing code anywhere in the mod (registered or unregistered patches) that handles plant removal — this feature was never written.
