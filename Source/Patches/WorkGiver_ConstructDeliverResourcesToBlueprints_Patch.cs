using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HomeMover
{
    /// <summary>
    /// Patch the original so pawn won't start a reinstall job if the subject building is the only support for nearby roof.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class WorkGiver_ConstructDeliverResourcesToBlueprints_Patch
    {
        private static readonly Harmony harmony = new Harmony("Jellypowered.HomeMover");

        static WorkGiver_ConstructDeliverResourcesToBlueprints_Patch()
        {
            ApplyPatches();
        }

        private static void ApplyPatches()
        {
            MethodInfo original = typeof(WorkGiver_ConstructDeliverResourcesToBlueprints).GetMethod(
                "JobOnThing",
                BindingFlags.Public | BindingFlags.Instance
            );
            MethodInfo postfix =
                typeof(WorkGiver_ConstructDeliverResourcesToBlueprints_Patch).GetMethod(
                    "Postfix",
                    BindingFlags.Public | BindingFlags.Static
                );

            if (original != null && postfix != null)
            {
                harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                HomeMoverMod.DebugLog("JobOnThing Patch applied.");
            }
            else
            {
                HomeMoverMod.DebugLog(
                    "Failed to apply JobOnThing Patch. Original method or postfix not found."
                );
            }
        }

        /// <summary>
        /// Postfix method.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="forced"></param>
        /// <param name="__result"></param>
        public static void Postfix(Thing t, bool forced, ref Job __result)
        {
            if (
                t is Blueprint_Install install
                && install.MiniToInstallOrBuildingToReinstall is Building building
                && building.def.holdsRoof
            )
            {
                HomeMoverMod.DebugLog(
                    $"Applying postfix for blueprint {install} and building {building}"
                );

                if (forced)
                {
                    DesignatorHomeMover.AddBeingReinstalledBuilding(building);
                    return;
                }

                if (
                    building.MapHeld.designationManager.DesignationOn(
                        building,
                        HomeMoverDefOf.HomeMover
                    ) != null
                )
                {
                    HomeMoverMod.DebugLog($"Building {building} has a MoveBase designation.");

                    bool canRemove = true;
                    HashSet<IntVec3> roofInRange = building.RoofInRange();
                    List<Building> buildingsBeingRemoved = DesignatorHomeMover
                        .GetBuildingsBeingReinstalled(building)
                        .Concat(new[] { building })
                        .ToList();

                    foreach (IntVec3 roof in roofInRange)
                    {
                        HomeMoverMod.DebugLog($"Checking roof at position {roof}.");
                        if (!roof.IsSupported(building.MapHeld, buildingsBeingRemoved))
                        {
                            HomeMoverMod.DebugLog($"Roof at position {roof} is not supported.");

                            building.MapHeld.areaManager.NoRoof[roof] = true;
                            building.MapHeld.areaManager.BuildRoof[roof] = false;
                            DesignatorHomeMover.AddToRoofToRemove(roof, building);
                            canRemove = false;
                        }
                    }

                    if (canRemove)
                    {
                        HomeMoverMod.DebugLog($"Can remove building {building}.");
                        DesignatorHomeMover.AddBeingReinstalledBuilding(building);
                    }
                    else
                    {
                        HomeMoverMod.DebugLog(
                            $"Cannot remove building {building}. Job result set to null."
                        );
                        __result = null;
                    }
                }
            }
        }
    }
}
