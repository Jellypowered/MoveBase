using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HomeMover
{
    [HarmonyPatch(typeof(DesignatorManager), nameof(DesignatorManager.Deselect))]
    public static class Designator_Deselect_Patch
    {
        public static void Prefix(DesignatorManager __instance)
        {
            if (
                __instance.SelectedDesignator is DesignatorHomeMover moveBase
                && !moveBase.KeepDesignation
                && moveBase.DesignatedThings.Any()
            )
            {
                foreach (Thing thing in moveBase.DesignatedThings)
                {
                    moveBase.Map.designationManager.TryRemoveDesignationOn(
                        thing,
                        HomeMoverDefOf.HomeMover
                    );
                }
            }
        }
    }
}
