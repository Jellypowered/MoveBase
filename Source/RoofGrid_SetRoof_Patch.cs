using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace MoveBase
{
    [StaticConstructorOnStartup]
    public static class RoofGrid_SetRoof_Patch
    {
        private static readonly Harmony harmony = new Harmony("com.movebase.harmony");

        static RoofGrid_SetRoof_Patch()
        {
            ApplyPatches();
        }

        private static void ApplyPatches()
        {
            MethodInfo original = typeof(RoofGrid).GetMethod("SetRoof", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo postfix = typeof(RoofGrid_SetRoof_Patch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);

            if (original != null && postfix != null)
            {
                harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                MoveBaseMod.DebugLog("SetRoof Patch applied.");
            }
            else
            {
                MoveBaseMod.DebugLog("Failed to apply SetRoof Patch. Original method or postfix not found.");
            }
        }

        public static void Postfix(RoofGrid __instance, IntVec3 c, RoofDef def)
        {
            if (def == null && Current.ProgramState == ProgramState.Playing)
            {
                DesignatorMoveBase.SetNoRoofFalse(c);
                MoveBaseMod.DebugLog($"Roof removed at {c}, SetNoRoofFalse called.");
            }
        }
    }
}