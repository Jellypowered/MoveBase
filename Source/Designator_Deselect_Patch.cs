using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoveBase
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
                __instance.SelectedDesignator is DesignatorMoveBase moveBase
                && !moveBase.KeepDesignation
                && moveBase.DesignatedThings.Any()
            )
            {
                foreach (Thing thing in moveBase.DesignatedThings)
                {
                    moveBase.Map.designationManager.TryRemoveDesignationOn(
                        thing,
                        MoveBaseDefOf.MoveBase
                    );
                }
            }
        }
    }
}
