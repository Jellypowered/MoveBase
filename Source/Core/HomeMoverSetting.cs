using Verse;

namespace HomeMover
{
    public class HomeMoverSetting : ModSettings
    {
        public bool enableDebugLogging = false;
        public bool skipItemsWithErrors = true;
        public bool showSkippedItemsMessage = true;
        public bool smartDependencyPlacement = true;
        public bool copyFloorTypes = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableDebugLogging, "enableDebugLogging", false);
            Scribe_Values.Look(ref skipItemsWithErrors, "skipItemsWithErrors", true);
            Scribe_Values.Look(ref showSkippedItemsMessage, "showSkippedItemsMessage", true);
            Scribe_Values.Look(ref smartDependencyPlacement, "smartDependencyPlacement", true);
            Scribe_Values.Look(ref copyFloorTypes, "copyFloorTypes", true);
        }

        public void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "Smart dependency placement (infrastructure placed after structures)",
                ref smartDependencyPlacement,
                "When enabled, wall attachments and conduits are automatically placed AFTER structures. This prevents most placement errors."
            );
            listing.CheckboxLabeled(
                "Skip items that can't be placed (instead of aborting entire move)",
                ref skipItemsWithErrors,
                "When enabled, items that have placement errors will be skipped. When disabled, the entire move will be cancelled if any item can't be placed."
            );
            if (skipItemsWithErrors)
            {
                listing.CheckboxLabeled(
                    "Show message for skipped items",
                    ref showSkippedItemsMessage,
                    "Display a message listing items that were skipped due to errors."
                );
            }
            listing.CheckboxLabeled(
                "Copy floor types to destination",
                ref copyFloorTypes,
                "When enabled, queues construction of floors at the destination to match the selected area."
            );
            listing.CheckboxLabeled(
                "Enable debug logging (requires dev mode)",
                ref enableDebugLogging
            );
            listing.End();
        }
    }
}
