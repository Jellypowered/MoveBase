using RimWorld;
using Verse;

namespace HomeMover
{
    public enum PlacementTier
    {
        Conduit = 0,
        Structure = 1,
        Door = 2,
        WallMount = 3,
        Furniture = 4,
        SmallItem = 5,
    }

    public static class PlacementOrder
    {
        public static PlacementTier GetPlacementTier(this Thing thing)
        {
            if (thing?.def == null)
                return PlacementTier.Furniture;

            ThingDef def = thing.def;

            if (IsConduitLike(def))
                return PlacementTier.Conduit;

            if (def.building != null && def.building.isAttachment)
                return PlacementTier.WallMount;

            if (def.thingClass != null && typeof(Building_Door).IsAssignableFrom(def.thingClass))
                return PlacementTier.Door;

            if (def.holdsRoof && def.building != null && def.building.isEdifice)
                return PlacementTier.Structure;

            if (RequiresWallSupport(def))
                return PlacementTier.WallMount;

            if (def.building != null && !def.building.isEdifice)
                return PlacementTier.SmallItem;

            return PlacementTier.Furniture;
        }

        public static bool IsConduitLike(this ThingDef def)
        {
            if (def?.building == null)
                return false;

            if (def.building.isAttachment)
                return false;

            if (def.building.isEdifice)
                return false;

            if (def.EverTransmitsPower)
                return true;

            if (def.designationCategory != null && def.designationCategory.defName == "Power")
                return true;

            if (def.defName != null && def.defName.ToLowerInvariant().Contains("conduit"))
                return true;

            return false;
        }

        public static bool RequiresWallSupport(this ThingDef def)
        {
            if (def?.building == null)
                return false;

            if (def.building.canPlaceOverWall)
                return true;

            if (def.PlaceWorkers == null)
                return false;

            foreach (PlaceWorker placeWorker in def.PlaceWorkers)
            {
                string workerName = placeWorker.GetType().Name;
                if (
                    workerName.Contains("Wall")
                    || workerName.Contains("Attach")
                    || workerName.Contains("OnWall")
                )
                    return true;
            }

            return false;
        }
    }
}
