using HarmonyLib;
using RimWorld;
using Verse;

namespace HomeMover
{
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.Destroy))]
    public static class Blueprint_Destroy_Patch
    {
        public static void Postfix(ThingWithComps __instance)
        {
            if (
                __instance is Blueprint_Install blueprint
                && blueprint.MiniToInstallOrBuildingToReinstall != null
                && blueprint.MiniToInstallOrBuildingToReinstall.MapHeld != null
            )
            {
                blueprint.MiniToInstallOrBuildingToReinstall.MapHeld.designationManager.TryRemoveDesignationOn(
                    blueprint.MiniToInstallOrBuildingToReinstall,
                    HomeMoverDefOf.HomeMover
                );
            }
        }
    }

    [HarmonyPatch(typeof(Blueprint_Install), nameof(Blueprint_Install.TryReplaceWithSolidThing))]
    public static class Blueprint_Install_TryReplace_Patch
    {
        public static void Postfix(Thing createdThing)
        {
            if (
                createdThing != null
                && createdThing is Building building
                && building.def != null
                && building.def.holdsRoof
            )
            {
                DesignatorHomeMover.RemoveBuildingFromCache(building);
            }
        }
    }
}
