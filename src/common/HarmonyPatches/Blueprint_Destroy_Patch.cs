﻿using System;
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
    public static class Blueprint_Destroy_Patch
    {
        static Blueprint_Destroy_Patch()
        {
            MethodInfo original = typeof(ThingWithComps).GetMethod("Destroy", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo postfix = typeof(Blueprint_Destroy_Patch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);
            HarmonyUtility.Instance.Patch(original, prefix: new HarmonyMethod(postfix));

            MethodInfo originalTryReplaceWithSolidThing = typeof(Blueprint_Install).GetMethod("TryReplaceWithSolidThing", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo postfixTryReplaceWithSolidThing = typeof(Blueprint_Destroy_Patch).GetMethod("PostfixTryReplaceWithSolidThing", BindingFlags.Static | BindingFlags.Public);
            HarmonyUtility.Instance.Patch(originalTryReplaceWithSolidThing, postfix: new HarmonyMethod(postfixTryReplaceWithSolidThing));
        }

        public static void Postfix(ThingWithComps __instance)
        {
            if (__instance is Blueprint_Install blueprint)
            {
                blueprint.MiniToInstallOrBuildingToReinstall?.MapHeld?.designationManager.TryRemoveDesignationOn(blueprint.MiniToInstallOrBuildingToReinstall, MoveBaseDefOf.MoveBase);
            }
        }

        public static void PostfixTryReplaceWithSolidThing(Thing createdThing)
        {
            if (createdThing is Building building && building.def.holdsRoof)
                DesignatorMoveBase.RemoveBuildingFromCache(building);
        }
    }
}
