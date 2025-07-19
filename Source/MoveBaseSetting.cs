using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace MoveBase
{
    public class MoveBaseSetting : ModSettings
    {
        public bool enableDebugLogging = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableDebugLogging, "enableDebugLogging", false);
        }

        public void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "Enable debug logging (requires dev mode)",
                ref enableDebugLogging
            );
            listing.End();
        }
    }
}
