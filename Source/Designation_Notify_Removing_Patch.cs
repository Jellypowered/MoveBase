using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MoveBase
{
    [StaticConstructorOnStartup]
    public static class Designation_Notify_Removing_Patch
    {
        static Designation_Notify_Removing_Patch()
        {
            MethodInfo original = typeof(Designation).GetMethod("Notify_Removing", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo postfix = typeof(Designation_Notify_Removing_Patch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);
        }

        public static void Postfix(Designation __instance)
        {
            if (__instance.def == MoveBaseDefOf.MoveBase && __instance.target.Thing != null)
            {
                DesignatorMoveBase.Notify_Removing_Callback(__instance.target.Thing);
                InstallBlueprintUtility.CancelBlueprintsFor(__instance.target.Thing);
            }
        }
    }
}
