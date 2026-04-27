# Code Audit Fixes

Applied during April 2026 audit pass. All changes are safe non-functional cleanups unless noted.

## Logic Bugs Fixed

### `RemoveEmptyCache` — inverted condition
**File:** `Source/DesignatorMoveBase.cs`

The cache removal predicate had `!` on both conditions, which caused it to remove models that were *active* (had buildings or roofs queued) and *keep* models that were empty. Fixed by removing both negations.

```csharp
// Before (wrong — removes active models):
if (!model.BuildingsToReinstall.EnumerableNullOrEmpty() && !model.RoofToRemove.EnumerableNullOrEmpty())

// After (correct — removes completed/empty models):
if (model.BuildingsToReinstall.EnumerableNullOrEmpty() && model.RoofToRemove.EnumerableNullOrEmpty())
```

## Dead Code Removed

### `DesignateThing` — unused variable
**File:** `Source/DesignatorMoveBase.cs`

`Designation designation = map.designationManager.AddDesignation(...)` — the return value was assigned but never read. Removed the variable; the `AddDesignation` call is kept.

### `DoExtraGuiControls` — `Rand.Int` as window ID
**File:** `Source/DesignatorMoveBase.cs`

`Rand.Int` was used as the `ImmediateWindow` ID, generating a new random value every frame. This causes the window to never be stable. Replaced with a named constant:

```csharp
private const int RotationControlWindowId = 743256897;
```

### `GameSaveComponent` — unused `_mod` field and empty override
**File:** `Source/GameSaveComponent.cs`

- `private MoveBaseMod _mod` was assigned in the constructor but never read. Removed.
- `FinalizeInit()` override only called `base.FinalizeInit()`. Removed.

### `GenConstruct_CanPlaceBlueprintAt_Patch` — unused `_designatorDef` field
**File:** `Source/GenConstruct_CanPlaceBlueprintAt_Patch.cs`

`_designatorDef` was fetched via reflection and cached in a static field but never used anywhere. Removed.

### `JobDriver_Uninstall_FinishedRemoving_Patch` — unused `_tickListNormal` field
**File:** `Source/JobDriver_Uninstall_FinishedRemoving_Patch.cs`

`_tickListNormal` was fetched via reflection and cached but never used. Removed. The dead static constructor that held it was also removed.

### `DesignatorMoveBase` — redundant third list
**File:** `Source/DesignatorMoveBase.cs`

`infrastructureSecond` was copied into a third `orderedThings` list unnecessarily. Replaced with `structuresFirst.AddRange(infrastructureSecond)` directly.

### Commented-out `_draggableDimension` lines
**File:** `Source/DesignatorMoveBase.cs`

Two commented-out `_draggableDimension` assignments (one in `Selected()`, one in `FinalizeDesignationSucceeded()`) were removed.

## Unused `using` Directives Removed

Across 13 source files the following were trimmed:

| Namespace | Files affected |
|---|---|
| `System.Text` | Multiple |
| `System.Threading.Tasks` | Multiple |
| `System.Collections.Concurrent` | `DesignatorMoveBase.cs` |
| `RimWorld.Planet` | `DesignatorMoveBase.cs` |
| `Verse.AI` | `DesignatorMoveBase.cs` |
| `HarmonyLib` | `MoveBaseMod.cs` |
| `UnityEngine` | `Designator_Deselect_Patch.cs` |
| Various `System.*` | Several patch files |

> **Note:** `using RimWorld` was accidentally removed from `HarmonyUtility.cs` and immediately restored; `Building_Door` (RimWorld type) is referenced there.
