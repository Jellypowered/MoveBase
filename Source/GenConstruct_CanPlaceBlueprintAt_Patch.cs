using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MoveBase
{
    /// <summary>
    /// Patch CanPlaceBlueprint so players can move buildings to positions where are occupied by other buildings that are also desiganted.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class GenConstruct_CanPlaceBlueprintAt_Patch
    {
        private static PropertyInfo _designatorDef = typeof(Designator).GetProperty(
            "DesignationDef ",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

        private static MethodInfo _thingInDesignation =
            typeof(GenConstruct_CanPlaceBlueprintAt_Patch).GetMethod(
                nameof(GenConstruct_CanPlaceBlueprintAt_Patch.ThingInDesignation),
                BindingFlags.NonPublic | BindingFlags.Static
            );

        private static MethodInfo _sameConduit =
            typeof(GenConstruct_CanPlaceBlueprintAt_Patch).GetMethod(
                nameof(GenConstruct_CanPlaceBlueprintAt_Patch.SameConduit),
                BindingFlags.NonPublic | BindingFlags.Static
            );

        private static object _retPos1;
        private static object _retPos2;
        private static object _retPos3;
        private static object _retPos4;
        private static object _retPos5;

        private static bool _matched1 = false;
        private static bool _matched2 = false;
        private static bool _matched3 = false;
        private static bool _matched4 = false;
        private static bool _matched5 = false;

        /// <summary>
        /// Match "identical thing exist" check.
        /// </summary>
        private static List<CodeInstruction> _patternToMatch1 = new List<CodeInstruction>()
        {
            new CodeInstruction(OpCodes.Ldloc_S, 8),
            new CodeInstruction(OpCodes.Ldarg_S),
            new CodeInstruction(OpCodes.Beq_S),
        };

        /// <summary>
        /// Match "space already occupied" check.
        /// </summary>
        private static List<CodeInstruction> _patternToMatch2 = new List<CodeInstruction>()
        {
            new CodeInstruction(OpCodes.Ldloc_S, 25),
            new CodeInstruction(OpCodes.Ldarg_S),
            new CodeInstruction(OpCodes.Beq_S),
        };

        /// <summary>
        /// Match "Interaction cell block" from check on designated thing.
        /// </summary>
        private static List<CodeInstruction> _patternToMatch3 = new List<CodeInstruction>()
        {
            new CodeInstruction(OpCodes.Ldloc_S, 10),
            new CodeInstruction(OpCodes.Ldloc_S, 11),
            new CodeInstruction(OpCodes.Callvirt),
            new CodeInstruction(OpCodes.Ldarg_S),
            new CodeInstruction(OpCodes.Beq),
        };

        private static List<CodeInstruction> _patternToMatch4 = new List<CodeInstruction>()
        {
            new CodeInstruction(OpCodes.Ldloc_S, 17),
            new CodeInstruction(OpCodes.Ldarg_S),
            new CodeInstruction(OpCodes.Beq),
        };

        private static List<CodeInstruction> _patternToMatch5 = new List<CodeInstruction>()
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(
                OpCodes.Callvirt,
                typeof(BuildableDef).GetProperty(nameof(BuildableDef.PlaceWorkers)).GetAccessors()[
                    0
                ]
            ),
            new CodeInstruction(OpCodes.Brfalse_S),
        };

        /// <summary>
        /// Operation mode for <see cref="GenConstruct.CanPlaceBlueprintAt(BuildableDef, IntVec3, Rot4, Map, bool, Thing, Thing, ThingDef)"/>.
        /// </summary>
        public static BlueprintMode Mode = BlueprintMode.Check;

        static GenConstruct_CanPlaceBlueprintAt_Patch()
        {
            MethodInfo original = typeof(GenConstruct).GetMethod(
                nameof(GenConstruct.CanPlaceBlueprintAt),
                BindingFlags.Static | BindingFlags.Public
            );
            MethodInfo transpiler = typeof(GenConstruct_CanPlaceBlueprintAt_Patch).GetMethod(
                "Transpiler",
                BindingFlags.Static | BindingFlags.Public
            );
        }

        /// <summary>
        /// Add a check in the method body so it would return true if thing occupies the current cell is also designated.
        /// </summary>
        /// <param name="codeInstructions"></param>
        /// <returns> Sequence of IL after patched. </returns>
        public static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> codeInstructions
        )
        {
            List<CodeInstruction> instructions = codeInstructions.ToList();
            for (int i = 0; i < instructions.Count; i++)
            {
                yield return instructions[i];

                if (
                    !_matched1
                    && HarmonyUtility.MatchPattern(
                        instructions,
                        _patternToMatch1,
                        i,
                        () =>
                        {
                            _retPos1 = (Label)instructions[i + _patternToMatch1.Count - 1].operand;
                            _matched1 = true;
                        }
                    )
                )
                {
                    foreach (
                        var c in HarmonyUtility.ReturnPatternMatchedInstruction(
                            _patternToMatch1,
                            instructions,
                            ref i
                        )
                    )
                        yield return c;

                    yield return new CodeInstruction(OpCodes.Ldloc_S, 8);
                    yield return new CodeInstruction(OpCodes.Call, _thingInDesignation);
                    yield return new CodeInstruction(OpCodes.Brtrue, _retPos1);
                }

                if (
                    !_matched2
                    && HarmonyUtility.MatchPattern(
                        instructions,
                        _patternToMatch2,
                        i,
                        () =>
                        {
                            _retPos2 = (Label)instructions[i + _patternToMatch2.Count - 1].operand;
                            _matched2 = true;
                        }
                    )
                )
                {
                    foreach (
                        var c in HarmonyUtility.ReturnPatternMatchedInstruction(
                            _patternToMatch2,
                            instructions,
                            ref i
                        )
                    )
                        yield return c;

                    yield return new CodeInstruction(OpCodes.Ldloc_S, 25);
                    yield return new CodeInstruction(OpCodes.Call, _thingInDesignation);
                    yield return new CodeInstruction(OpCodes.Brtrue, _retPos2);
                }

                if (
                    !_matched3
                    && HarmonyUtility.MatchPattern(
                        instructions,
                        _patternToMatch3,
                        i,
                        () =>
                        {
                            _retPos3 = (Label)instructions[i + _patternToMatch3.Count - 1].operand;
                            _matched3 = true;
                        }
                    )
                )
                {
                    foreach (
                        var c in HarmonyUtility.ReturnPatternMatchedInstruction(
                            _patternToMatch3,
                            instructions,
                            ref i
                        )
                    )
                        yield return c;

                    yield return instructions[i - _patternToMatch3.Count + 1];
                    yield return instructions[i - _patternToMatch3.Count + 2];
                    yield return instructions[i - _patternToMatch3.Count + 3];
                    yield return new CodeInstruction(OpCodes.Call, _thingInDesignation);
                    yield return new CodeInstruction(OpCodes.Brtrue, _retPos3);
                }

                if (
                    !_matched4
                    && HarmonyUtility.MatchPattern(
                        instructions,
                        _patternToMatch4,
                        i,
                        () =>
                        {
                            _retPos4 = (Label)instructions[i + _patternToMatch4.Count - 1].operand;
                            _matched4 = true;
                        }
                    )
                )
                {
                    foreach (
                        var c in HarmonyUtility.ReturnPatternMatchedInstruction(
                            _patternToMatch4,
                            instructions,
                            ref i
                        )
                    )
                        yield return c;

                    yield return instructions[i - _patternToMatch4.Count + 1];
                    yield return new CodeInstruction(OpCodes.Call, _thingInDesignation);
                    yield return new CodeInstruction(OpCodes.Brtrue, _retPos4);
                }

                if (
                    !_matched5
                    && HarmonyUtility.MatchPattern(
                        instructions,
                        _patternToMatch5,
                        i,
                        () =>
                        {
                            _retPos5 = (Label)instructions[i + _patternToMatch5.Count - 1].operand;
                            _matched5 = true;
                        }
                    )
                )
                {
                    foreach (
                        var c in HarmonyUtility.ReturnPatternMatchedInstruction(
                            _patternToMatch5,
                            instructions,
                            ref i
                        )
                    )
                        yield return c;

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_3);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, _sameConduit);
                    yield return new CodeInstruction(OpCodes.Brtrue, _retPos5);
                }
            }
        }

        private static bool ThingInDesignation(Thing thing)
        {
            if (Find.DesignatorManager.SelectedDesignator is DesignatorMoveBase move)
            {
                if (Mode == BlueprintMode.Check && move.DesignatedThings.Contains(thing))
                    return true;
            }

            return false;
        }

        private static bool SameConduit(BuildableDef def, Map map, IntVec3 loc)
        {
            List<Thing> things = loc.GetThingList(map);
            if (things.Any(t => t.def == def && ThingInDesignation(t)))
                return true;

            return false;
        }
    }

    /// <summary>
    /// Blueprint mode that controls how <see cref="GenConstruct.CanPlaceBlueprintAt(BuildableDef, IntVec3, Rot4, Map, bool, Thing, Thing, ThingDef)"/> is executed.
    /// </summary>
    public enum BlueprintMode
    {
        /// <summary>
        /// Mode for checking if blueprint can be placed at a cell.
        /// </summary>
        Check,

        /// <summary>
        /// Mode for placing down blueprint on a cell.
        /// </summary>
        Place,
    }
}
