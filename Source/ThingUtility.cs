using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace MoveBase
{
    public static class ThingUtility
    {
        public static HashSet<IntVec3> RoofInRange(this Building building)
        {
            HashSet<IntVec3> supportedRoof = new HashSet<IntVec3>();
            Map map = building.MapHeld;
            map.floodFiller.FloodFill(
                building.Position
                , (cell) => cell.InHorDistOf(building.Position, RoofCollapseUtility.RoofMaxSupportDistance)
                , (cell) =>
                {
                    if (cell.Roofed(map))
                        supportedRoof.Add(cell);
                }
                , extraRoots: building.OccupiedRect());

            return supportedRoof;
        }

        public static Rot4 Rotate(this Thing thing, Rot4 rotation)
        {
            if (thing.def.rotatable)
                return new Rot4(rotation.AsInt + thing.Rotation.AsInt);

            return thing.Rotation;
        }

        public static bool IdenticalWith(this Thing thing, Rot4 extraRotation, Thing other)
        {
            if (!(thing.def == other.def && thing.Stuff == other.Stuff))
                return false;

            if (thing.def == ThingDefOf.Wall)
                return true;

            thing.TryGetQuality(out QualityCategory qc1);
            other.TryGetQuality(out QualityCategory qc2);

            if (qc1 != qc2)
                return false;

            if (thing.Rotate(extraRotation) != other.Rotation)
                return false;

            return true;
        }

        /// <summary>
        /// Check if <paramref name="thing"/> blocks any interaction cell of others if placed at <paramref name="pos"/> with rotation <paramref name="rot"/>.
        /// </summary>
        /// <param name="thing"> Thing to place. </param>
        /// <param name="pos"> Position at which <paramref name="thing"/> would be placed. </param>
        /// <param name="rot"> Would be rotation of <paramref name="thing"/>. </param>
        /// <returns> First Thing which interaction cell is blocked by <paramref name="thing"/>. </returns>
        public static Thing BlockAdjacentInteractionCell(this Thing thing, IntVec3 pos, Rot4 rot)
        {
            CellRect cellRect = GenAdj.OccupiedRect(pos, rot, thing.def.size);
            foreach (IntVec3 cell in GenAdj.CellsAdjacentCardinal(pos, rot, thing.def.Size))
            {
                if (cell.InBounds(thing.MapHeld))
                {
                    List<Thing> things = cell.GetThingList(thing.MapHeld);
                    foreach (Thing neighbour in things)
                    {
                        if (neighbour.def.hasInteractionCell
                            && (thing.def.passability == Traversability.Impassable || thing.def == neighbour.def)
                            && cellRect.Contains(
                                Verse.ThingUtility.InteractionCellWhenAt(
                                    neighbour.def, neighbour.Position, neighbour.Rotation, neighbour.Map)))
                        {
                            return neighbour;
                        }
                    }
                }
            }

            return null;
        }
    }
}
