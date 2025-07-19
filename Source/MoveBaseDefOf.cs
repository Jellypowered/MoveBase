using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace MoveBase
{
    [DefOf]
    public static class MoveBaseDefOf
    {
        public static DesignationDef MoveBase;

        static MoveBaseDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MoveBaseDefOf));
        }
    }
}
