using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HomeMover
{
    [StaticConstructorOnStartup]
    public static class Blueprint_Destroy_Patch
    {
        static Blueprint_Destroy_Patch()
        {
            MethodInfo original = typeof(ThingWithComps).GetMethod(
                "Destroy",
                BindingFlags.Public | BindingFlags.Instance
            );
            MethodInfo postfix = typeof(Blueprint_Destroy_Patch).GetMethod(
                "Postfix",
                BindingFlags.Static | BindingFlags.Public
            );

            MethodInfo originalTryReplaceWithSolidThing = typeof(Blueprint_Install).GetMethod(
                "TryReplaceWithSolidThing",
                BindingFlags.Public | BindingFlags.Instance
            );
            MethodInfo postfixTryReplaceWithSolidThing = typeof(Blueprint_Destroy_Patch).GetMethod(
                "PostfixTryReplaceWithSolidThing",
                BindingFlags.Static | BindingFlags.Public
            );
        }

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

        public static void PostfixTryReplaceWithSolidThing(Thing createdThing)
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
