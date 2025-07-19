﻿using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MoveBase
{
    /// <summary>
    /// Fixes tick-related exceptions when minifying buildings, particularly doors.
    /// Applies early deregistration and despawn if the object has a MoveBase designation.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MinifyUtility_MakeMinified
    {

        private static readonly FieldInfo _tickListNormal =
            typeof(TickManager).GetField("tickListNormal", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(typeof(MinifyUtility), nameof(MinifyUtility.MakeMinified))]
        public static class MinifyUtility_MakeMinified_Patch
        {
            public static bool Prefix(Thing thing, DestroyMode destroyMode, out bool __state)
            {
                __state = thing?.MapHeld?.designationManager?.DesignationOn(thing, MoveBaseDefOf.MoveBase) != null;

                if (__state && thing is Building building && building.Spawned)
                {
                    MarkBeingMinified(building);
                    
                    // Remove from normal tick list
                    TickList tickList = _tickListNormal?.GetValue(Find.TickManager) as TickList;
                    tickList?.DeregisterThing(building);

                    // Remove from all tick lists
                    DeregisterFromAllTickLists(building);

                    // Despawn early to prevent errors during vanilla flow
                    building.DeSpawn(DestroyMode.Vanish);
                }

                return true; // Proceed to original method
            }

            public static void Postfix(Thing thing, DestroyMode destroyMode, MinifiedThing __result, bool __state)
            {
                if (__state && __result != null)
                {
                    ClearBeingMinified(thing);
                    MoveBase_DelayedCleanup.Queue(thing);
                    //MoveBaseMod.DebugLog($"Queued delayed tick cleanup for {thing}");
                }
            }
        }

        /// <summary>
        /// Prevents NREs when doors are ticking but aren't fully initialized.
        /// </summary>
        [HarmonyPatch(typeof(Building_Door), "Tick")]
        public static class Building_Door_Tick_Patch
        {
            public static bool Prefix(Building_Door __instance)
            {
                if (MinifyUtility_MakeMinified.IsBeingMinified(__instance))
                {
                    //MoveBaseMod.DebugLog($"Skipped ticking door {__instance} (currently being minified).");
                    return false;
                }

                //MoveBaseMod.DebugLog($"Ticking door {__instance} with Map={__instance.Map}, Spawned={__instance.Spawned}");

                if (__instance.Map == null || !__instance.Spawned)
                {
                //    MoveBaseMod.DebugLog($"Skipped ticking door {__instance} due to null map or unspawned.");
                    return false;
                }

                return true;
            }
        }
        public static class MoveBase_DelayedCleanup
        {
            private static readonly List<Thing> toCleanup = new List<Thing>();

            public static void Queue(Thing thing)
            {
                if (thing != null && !toCleanup.Contains(thing))
                    toCleanup.Add(thing);
            }

            public static void Tick()
            {
                if (toCleanup.Count == 0) return;

                foreach (var thing in toCleanup)
                {
                    if (thing is Building building)
                    {
                        MinifyUtility_MakeMinified.DeregisterFromAllTickLists(building);
                        //MoveBaseMod.DebugLog($"Delayed cleanup for {building}");
                    }
                }

                toCleanup.Clear();
            }
        }
        [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
        public static class TickManager_DoSingleTick_Patch
        {
            public static void Postfix()
            {
                MoveBase_DelayedCleanup.Tick();
            }
        }

        private static readonly HashSet<Thing> beingMinified = new HashSet<Thing>();

        public static void MarkBeingMinified(Thing thing)
        {
            if (thing != null) beingMinified.Add(thing);
        }


        public static bool IsBeingMinified(Thing thing)
        {
            return thing != null && beingMinified.Contains(thing);
        }

        public static void ClearBeingMinified(Thing thing)
        {
            if (thing != null) beingMinified.Remove(thing);
        }

        /// <summary>
        /// Safely deregisters a thing from all three tick lists.
        /// </summary>
        public static void DeregisterFromAllTickLists(Thing thing)
        {
            var tickManager = Find.TickManager;
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var fieldNames = new[] { "tickListNormal", "tickListRare", "tickListLong" };
            var tickListThingsField = typeof(TickList).GetField("things", flags);

            foreach (var fieldName in fieldNames)
            {
                var field = tickManager.GetType().GetField(fieldName, flags);
                if (field == null) continue;

                var tickList = field.GetValue(tickManager) as TickList;
                if (tickList == null) continue;

                var things = tickListThingsField?.GetValue(tickList) as List<Thing>;
                if (things != null && things.Contains(thing))
                {
                    tickList.DeregisterThing(thing);
                }
            }
        } 
        }
    }