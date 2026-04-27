using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HomeMover
{
    [HarmonyPatch(typeof(JobDriver_Uninstall), "FinishedRemoving")]
    public static class JobDriver_Uninstall_FinishedRemoving_Patch
    {
        private static readonly PropertyInfo _building = typeof(JobDriver_RemoveBuilding).GetProperty(
            "Building",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        public static void Postfix(JobDriver_Uninstall __instance)
        {
            DesignatorHomeMover.UninstallJobCallback(
                (Building)_building.GetValue(__instance),
                __instance.pawn.MapHeld
            );
        }
    }
}
