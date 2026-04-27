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
        public bool handleObstructions = true;
        public bool autoMinifyBlockers = true;
        public bool warnNonMinifiable = true;
        public bool queueDestinationRoof = true;
        public bool allowThickRoofMoves = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enableDebugLogging, "enableDebugLogging", false);
            Scribe_Values.Look(ref skipItemsWithErrors, "skipItemsWithErrors", true);
            Scribe_Values.Look(ref showSkippedItemsMessage, "showSkippedItemsMessage", true);
            Scribe_Values.Look(ref smartDependencyPlacement, "smartDependencyPlacement", true);
            Scribe_Values.Look(ref copyFloorTypes, "copyFloorTypes", true);
            Scribe_Values.Look(ref handleObstructions, "handleObstructions", true);
            Scribe_Values.Look(ref autoMinifyBlockers, "autoMinifyBlockers", true);
            Scribe_Values.Look(ref warnNonMinifiable, "warnNonMinifiable", true);
            Scribe_Values.Look(ref queueDestinationRoof, "queueDestinationRoof", true);
            Scribe_Values.Look(ref allowThickRoofMoves, "allowThickRoofMoves", false);
        }

        public void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                UIText.SettingsSmartDependencyLabel.TranslateSimple(),
                ref smartDependencyPlacement,
                UIText.SettingsSmartDependencyDesc.TranslateSimple()
            );
            listing.CheckboxLabeled(
                UIText.SettingsSkipItemsLabel.TranslateSimple(),
                ref skipItemsWithErrors,
                UIText.SettingsSkipItemsDesc.TranslateSimple()
            );
            if (skipItemsWithErrors)
            {
                listing.CheckboxLabeled(
                    UIText.SettingsShowSkippedLabel.TranslateSimple(),
                    ref showSkippedItemsMessage,
                    UIText.SettingsShowSkippedDesc.TranslateSimple()
                );
            }
            listing.CheckboxLabeled(
                UIText.SettingsCopyFloorsLabel.TranslateSimple(),
                ref copyFloorTypes,
                UIText.SettingsCopyFloorsDesc.TranslateSimple()
            );
            listing.CheckboxLabeled(
                UIText.SettingsHandleObstructionsLabel.TranslateSimple(),
                ref handleObstructions,
                UIText.SettingsHandleObstructionsDesc.TranslateSimple()
            );
            if (handleObstructions)
            {
                listing.CheckboxLabeled(
                    UIText.SettingsAutoUninstallLabel.TranslateSimple(),
                    ref autoMinifyBlockers,
                    UIText.SettingsAutoUninstallDesc.TranslateSimple()
                );
                listing.CheckboxLabeled(
                    UIText.SettingsWarnNonMinifiableLabel.TranslateSimple(),
                    ref warnNonMinifiable,
                    UIText.SettingsWarnNonMinifiableDesc.TranslateSimple()
                );
            }
            listing.CheckboxLabeled(
                UIText.SettingsQueueRoofLabel.TranslateSimple(),
                ref queueDestinationRoof,
                UIText.SettingsQueueRoofDesc.TranslateSimple()
            );
            listing.CheckboxLabeled(
                UIText.SettingsAllowThickRoofLabel.TranslateSimple(),
                ref allowThickRoofMoves,
                UIText.SettingsAllowThickRoofDesc.TranslateSimple()
            );
            listing.CheckboxLabeled(
                UIText.SettingsDebugLoggingLabel.TranslateSimple(),
                ref enableDebugLogging
            );
            listing.End();
        }
    }
}
