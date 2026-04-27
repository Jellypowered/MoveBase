using RimWorld;
using Verse;

namespace HomeMover
{
    [DefOf]
    public static class HomeMoverDefOf
    {
        public static DesignationDef HomeMover;

        static HomeMoverDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HomeMoverDefOf));
        }
    }
}
