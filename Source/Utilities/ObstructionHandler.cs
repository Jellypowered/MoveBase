using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HomeMover
{
    public static class ObstructionHandler
    {
        private static readonly HashSet<int> WarnedBlockers = new HashSet<int>();

        public static void HandleObstructionsAt(
            Thing constructible,
            IntVec3 pos,
            Rot4 rot,
            Map map,
            ISet<Thing> moveGroup
        )
        {
            if (!HomeMoverMod.Setting.handleObstructions || constructible == null || map == null)
                return;

            foreach (IntVec3 cell in GenAdj.OccupiedRect(pos, rot, constructible.def.Size))
            {
                foreach (Thing blocker in cell.GetThingList(map).ToList())
                {
                    if (blocker == null || blocker.DestroyedOrNull() || blocker == constructible)
                        continue;

                    if (moveGroup != null && moveGroup.Contains(blocker))
                        continue;

                    if (!GenConstruct.BlocksConstruction(constructible, blocker))
                        continue;

                    if (TryHandlePlant(blocker, map))
                        continue;

                    if (TryHandleMineable(blocker, map))
                        continue;

                    if (TryHandlePlayerBuilding(blocker, map))
                        continue;

                    if (TryHandleCorpse(blocker, map))
                        continue;

                    if (TryHandleLooseItem(blocker, map))
                        continue;

                    WarnBlocker(blocker, UIText.BlockerCannotBeMoved.Translate(blocker.LabelCap));
                }
            }
        }

        private static bool TryHandlePlant(Thing blocker, Map map)
        {
            if (!(blocker is Plant plant))
                return false;

            DesignationDef desDef = plant.HarvestableNow
                ? DesignationDefOf.HarvestPlant
                : DesignationDefOf.CutPlant;

            if (map.designationManager.DesignationOn(plant, desDef) == null)
            {
                map.designationManager.AddDesignation(new Designation(plant, desDef));
            }

            return true;
        }

        private static bool TryHandleMineable(Thing blocker, Map map)
        {
            if (!blocker.def.mineable)
                return false;

            if (map.designationManager.DesignationOn(blocker, DesignationDefOf.Mine) == null)
            {
                map.designationManager.AddDesignation(new Designation(blocker, DesignationDefOf.Mine));
            }

            return true;
        }

        private static bool TryHandlePlayerBuilding(Thing blocker, Map map)
        {
            if (!(blocker is Building building) || building.Faction != Faction.OfPlayer)
                return false;

            if (building.def.Minifiable && HomeMoverMod.Setting.autoMinifyBlockers)
            {
                if (map.designationManager.DesignationOn(building, DesignationDefOf.Uninstall) == null)
                {
                    map.designationManager.AddDesignation(
                        new Designation(building, DesignationDefOf.Uninstall)
                    );
                }
                return true;
            }

            if (HomeMoverMod.Setting.warnNonMinifiable)
            {
                WarnBlocker(building, UIText.BlockerNotMinifiable.Translate(building.LabelCap));
            }

            return true;
        }

        private static bool TryHandleCorpse(Thing blocker, Map map)
        {
            if (!(blocker is Corpse corpse))
                return false;

            // Corpses can naturally be hauled by pawns; just accept them as "handled"
            // (They won't actually block most construction anyway)
            HomeMoverMod.DebugLog($"Corpse at {corpse.Position} will be naturally hauled out of the way");
            return true;
        }

        private static bool TryHandleLooseItem(Thing blocker, Map map)
        {
            // Loose items (not buildings, plants, or corpses)
            if (blocker is Building || blocker is Plant || blocker is Corpse || !blocker.Spawned)
                return false;

            // Most loose items naturally get moved by pawns; accept them as "handled"
            HomeMoverMod.DebugLog($"Item {blocker.LabelCap} at {blocker.Position} will be naturally moved out of the way");
            return true;
        }

        private static void WarnBlocker(Thing blocker, string text)
        {
            if (blocker == null || WarnedBlockers.Contains(blocker.thingIDNumber))
                return;

            WarnedBlockers.Add(blocker.thingIDNumber);
            Messages.Message(text, blocker, MessageTypeDefOf.CautionInput);
            HomeMoverMod.DebugLog($"Blocking thing warning: {blocker.LabelCap}");
        }
    }
}
