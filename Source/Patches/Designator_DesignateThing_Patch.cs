using HarmonyLib;
using RimWorld;
using Verse;

namespace HomeMover
{
    [HarmonyPatch(typeof(Designator_Cancel), nameof(Designator_Cancel.DesignateThing))]
    public static class Designator_CancelThing_Patch
    {
        public static void Prefix(Designator __instance, Thing t)
        {
            if (__instance is Designator_Cancel cancel)
            {
                if (t.MapHeld.designationManager.DesignationOn(t, HomeMoverDefOf.HomeMover) != null)
                {
                    DesignatorHomeMover.Notify_Removing_Callback(t);
                    InstallBlueprintUtility.CancelBlueprintsFor(t);
                }
            }
        }

    }

    [HarmonyPatch(typeof(Designator_Cancel), nameof(Designator_Cancel.DesignateSingleCell))]
    public static class Designator_CancelSingleCell_Patch
    {
        public static void Prefix(Designator __instance, IntVec3 c)
        {
            if (__instance is Designator_Cancel cancel)
            {
                foreach (Thing thing in c.GetThingList(__instance.Map))
                {
                    if (
                        thing.MapHeld.designationManager.DesignationOn(
                            thing,
                            HomeMoverDefOf.HomeMover
                        ) != null
                    )
                    {
                        DesignatorHomeMover.Notify_Removing_Callback(thing);
                        InstallBlueprintUtility.CancelBlueprintsFor(thing);
                    }
                }
            }
        }
    }
}
