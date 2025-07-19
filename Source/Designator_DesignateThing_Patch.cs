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
    public static class Designator_Patch
    {
        static Designator_Patch()
        {
            MethodInfo originalDesignateThing = typeof(Designator_Cancel).GetMethod("DesignateThing", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo designateThingPrefix = typeof(Designator_Patch).GetMethod("DesignateThingPrefix", BindingFlags.Static | BindingFlags.Public);
        
            MethodInfo originalDesignateSingleCell = typeof(Designator_Cancel).GetMethod("DesignateSingleCell", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo designteSingleCellPrefix = typeof(Designator_Patch).GetMethod("DesignateSingleCellPrefix", BindingFlags.Static | BindingFlags.Public);
        }

        public static void DesignateThingPrefix(Designator __instance, Thing t)
        {
            if (__instance is Designator_Cancel cancel)
            {
                if (t.MapHeld.designationManager.DesignationOn(t, MoveBaseDefOf.MoveBase) != null)
                {
                    DesignatorMoveBase.Notify_Removing_Callback(t);
                    InstallBlueprintUtility.CancelBlueprintsFor(t);
                }
            }
        }

        public static void DesignateSingleCellPrefix(Designator __instance, IntVec3 c)
        {
            if (__instance is Designator_Cancel cancel)
            {
                foreach (Thing thing in c.GetThingList(__instance.Map))
                {
                    if (thing.MapHeld.designationManager.DesignationOn(thing, MoveBaseDefOf.MoveBase) != null)
                    {
                        DesignatorMoveBase.Notify_Removing_Callback(thing);
                        InstallBlueprintUtility.CancelBlueprintsFor(thing);
                    }
                }
            }
        }
    }
}
