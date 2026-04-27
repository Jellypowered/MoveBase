using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HomeMover
{
    [StaticConstructorOnStartup]
    public static class Designator_Deselect_Patch
    {
        static Designator_Deselect_Patch()
        {
            MethodInfo original = typeof(DesignatorManager).GetMethod("Deselect");
            MethodInfo prefix = typeof(Designator_Deselect_Patch).GetMethod("Prefix");
        }

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
