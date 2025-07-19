using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MoveBase
{
    [StaticConstructorOnStartup]
    public static class JobDriver_Uninstall_FinishedRemoving_Patch
    {
        private static PropertyInfo _building = typeof(JobDriver_RemoveBuilding).GetProperty("Building", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _tickListNormal = typeof(TickManager).GetField("tickListNormal", BindingFlags.NonPublic | BindingFlags.Instance);

        static JobDriver_Uninstall_FinishedRemoving_Patch()
        {
            MethodInfo original = typeof(JobDriver_Uninstall).GetMethod("FinishedRemoving", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo postfix = typeof(JobDriver_Uninstall_FinishedRemoving_Patch).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
        }
        public static void Postfix(JobDriver_Uninstall __instance)
        {
            DesignatorMoveBase.UninstallJobCallback((Building)_building.GetValue(__instance), __instance.pawn.MapHeld);
        }
    }
}
