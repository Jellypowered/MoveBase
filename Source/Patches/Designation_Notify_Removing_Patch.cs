// Intentionally disabled.
//
// The original mod had a Designation.Notify_Removing postfix that called
// InstallBlueprintUtility.CancelBlueprintsFor(thing) whenever a HomeMover
// designation was removed — but it was COMMENTED OUT (never registered)
// in the original source. See modcompat/OriginalMB/HarmonyPatches/Designation_Notify_Removing_Patch.cs.
//
// When we converted patches to [HarmonyPatch]/PatchAll, this one became
// active and started firing on every legitimate designation cleanup
// (notably during MakeMinified, which removes the HomeMover designation
// from the source). The Cancel call there destroyed the destination
// Blueprint_Install — causing the "pairwise blueprint disappearance" bug
// where moving any item killed its destination blueprint.
//
// User-initiated Cancel is already handled by Designator_DesignateThing_Patch
// (Designator_CancelThing_Patch / Designator_CancelSingleCell_Patch).
// This file intentionally has no patches; it remains as a breadcrumb so the
// hook is not re-added.

namespace HomeMover
{
    internal static class Designation_Notify_Removing_Patch_Disabled
    {
    }
}
