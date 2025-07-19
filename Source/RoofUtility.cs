using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MoveBase
{
    public static class RoofUtility
    {
        private static Dictionary<IntVec3, Building> _supportedRoof = new Dictionary<IntVec3, Building>();

        /// <summary>
        /// Check if roof is supported by buildings other than those in <paramref name="exceptions"/>.
        /// </summary>
        public static bool IsSupported(this IntVec3 roof, Map map, IEnumerable<Thing> exceptions)
        {
            if (_supportedRoof.TryGetValue(roof, out Building cachedBuilding) && !exceptions.Contains(cachedBuilding))
                return true;

            bool supported = false;

            map.floodFiller.FloodFill(
                root: roof,
                passCheck: (IntVec3 cell) =>
                    cell.Roofed(map) && cell.InHorDistOf(roof, RoofCollapseUtility.RoofMaxSupportDistance),
                processor: (IntVec3 cell) =>
                {
                    Building edifice = cell.GetEdifice(map);
                    if (edifice != null && edifice.def.holdsRoof && !exceptions.Contains(edifice))
                    {
                        _supportedRoof[roof] = edifice;
                        supported = true;
                    }
                },
                maxCellsToProcess: 512); // Optional safety cap

            return supported;
        }
    }
}