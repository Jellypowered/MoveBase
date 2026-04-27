using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HomeMover
{
    /// <summary>
    /// Designator for Home Mover.
    /// </summary>
    [StaticConstructorOnStartup]
    public class DesignatorHomeMover : Designator
    {
        private static readonly MethodInfo _setBuildingToReinstall =
            typeof(Blueprint_Install).GetMethod(
                "SetBuildingToReinstall",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

        private static Texture2D _icon;

        private static Rot4 _rotation = Rot4.North;
        private static bool _originFound = false;
        private static IntVec3 _origin = IntVec3.Zero;
        private static HashSet<IntVec3> _selectedCells = new HashSet<IntVec3>();
        private static Dictionary<Thing, IntVec3> _ghostPos = new Dictionary<Thing, IntVec3>();
        private static Dictionary<IntVec3, TerrainDef> _floorPattern = new Dictionary<IntVec3, TerrainDef>();
        private static HashSet<Thing> _nonMinifiableThings = new HashSet<Thing>();
        private static List<RemoveRoofModel> _removeRoofModels = new List<RemoveRoofModel>();

        private static Mode _mode = Mode.Select;

        private const int RotationControlWindowId = 743256897;

        /// <summary>
        /// Gets or sets whether designation should be kept when the designator is deselected.
        /// </summary>
        public bool KeepDesignation { get; set; } = false;

        /// <summary>
        /// Gets or sets a list of designated things.
        /// </summary>
        public List<Thing> DesignatedThings { get; set; } = new List<Thing>();

        /// <summary>
        /// Dimension used by desigator when drag.
        /// </summary>
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.Orders;

        /// <summary>
        /// Gets the def for this designation.
        /// </summary>
        protected override DesignationDef Designation => HomeMoverDefOf.HomeMover;

        enum Mode
        {
            Select,
            Place,
        }

        /// <summary>
        /// Create instance of DesignatorMovebase.
        /// </summary>
        public DesignatorHomeMover()
        {
            this.icon = _icon ?? (_icon = ContentFinder<Texture2D>.Get("UI/Designations/MoveBase"));
            this.useMouseIcon = true;
            this.defaultDesc = UIText.Description.TranslateSimple();
            this.defaultLabel = UIText.Label.TranslateSimple();
        }

        public static void PlaceWaitingBuildings()
        {
            GenConstruct_CanPlaceBlueprintAt_Patch.Mode = BlueprintMode.Place;
            foreach (RemoveRoofModel model in _removeRoofModels)
            {
                HashSet<Thing> moveGroup = model.DesignatedThings?.ToHashSet() ?? new HashSet<Thing>();

                foreach (Thing thingToProcess in model.WaitingThings.ToList())
                {
                    if (thingToProcess.DestroyedOrNull() || thingToProcess.MapHeld != model.Map)
                    {
                        model.WaitingThings.Remove(thingToProcess);
                        continue;
                    }

                    // Start with the thing as-is, but we may force-minify it
                    Thing thingToPlace = thingToProcess;

                    IntVec3 deltaCell = GetDeltaCell(thingToPlace, model.MousePos, model.GhostPosition);

                    Thing inner = thingToPlace.GetInnerIfMinified();
                    if (
                        GenConstruct
                            .CanPlaceBlueprintAt(
                                inner.def,
                                deltaCell,
                                inner.Rotate(model.Rotation),
                                inner.MapHeld,
                                thing: inner
                            )
                            .Accepted
                    )
                    {
                        // For non-minifiable buildings in the selection, force-minify them so they can be moved
                        if (!(thingToPlace is MinifiedThing) && inner is Building building && !building.def.Minifiable)
                        {
                            try
                            {
                                MinifiedThing forceMinified = MinifyUtility.MakeMinified(building);
                                if (forceMinified != null)
                                {
                                    thingToPlace = forceMinified;
                                    inner = forceMinified.GetInnerIfMinified();
                                    HomeMoverMod.DebugLog($"Force-minified non-minifiable building: {building.LabelCap}");
                                }
                                else
                                {
                                    model.WaitingThings.Remove(thingToProcess);
                                    if (HomeMoverMod.Setting.warnNonMinifiable)
                                    {
                                        Messages.Message(
                                            UIText.ItemNotMinifiable.Translate(building.LabelCap),
                                            building,
                                            MessageTypeDefOf.CautionInput
                                        );
                                    }
                                    continue;
                                }
                            }
                            catch (Exception ex)
                            {
                                HomeMoverMod.DebugLog($"Failed to force-minify {building.LabelCap}: {ex.Message}");
                                model.WaitingThings.Remove(thingToProcess);
                                if (HomeMoverMod.Setting.warnNonMinifiable)
                                {
                                    Messages.Message(
                                        UIText.ItemNotMinifiable.Translate(building.LabelCap),
                                        building,
                                        MessageTypeDefOf.CautionInput
                                    );
                                }
                                continue;
                            }
                        }

                        // ?? Despawn it first
                        if (thingToPlace.Spawned)
                        {
                            thingToPlace.DeSpawn(DestroyMode.Vanish);
                        }
                        if (thingToPlace is MinifiedThing minifiedThing)
                            GenConstruct.PlaceBlueprintForInstall(
                                minifiedThing,
                                deltaCell,
                                minifiedThing.MapHeld,
                                inner.Rotate(model.Rotation),
                                Faction.OfPlayer
                            );
                        else
                            GenConstruct.PlaceBlueprintForReinstall(
                                thingToPlace as Building,
                                deltaCell,
                                thingToPlace.MapHeld,
                                thingToPlace.Rotate(model.Rotation),
                                Faction.OfPlayer
                            );

                        model.WaitingThings.Remove(thingToProcess);
                    }
                    else
                    {
                        ObstructionHandler.HandleObstructionsAt(
                            inner,
                            deltaCell,
                            inner.Rotate(model.Rotation),
                            inner.MapHeld,
                            moveGroup
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Clear cache of roof to remove.
        /// </summary>
        public static void ClearCache()
        {
            _removeRoofModels.Clear();
        }

        /// <summary>
        /// Save state.
        /// </summary>
        public static void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                RemoveEmptyCache();

            Scribe_Collections.Look(
                ref _removeRoofModels,
                nameof(_removeRoofModels),
                LookMode.Deep
            );
            _removeRoofModels = _removeRoofModels ?? new List<RemoveRoofModel>();
        }

        /// <summary>
        /// It should be invoked when a building is removed from designation.
        /// </summary>
        /// <param name="thing"> Building is being removed from designation. </param>
        public static void Notify_Removing_Callback(Thing thing)
        {
            if (!(thing is Building building) || !building.def.holdsRoof || !building.Spawned)
                return;

            foreach (RemoveRoofModel model in _removeRoofModels)
            {
                if (model.BuildingsToReinstall.Contains(building))
                {
                    IntVec3 pos = thing.Position;
                    List<IntVec3> workingSet = new List<IntVec3>(model.RoofToRemove);
                    foreach (IntVec3 roof in workingSet)
                    {
                        if (pos.InHorDistOf(roof, RoofCollapseUtility.RoofMaxSupportDistance))
                        {
                            model.RoofToRemove.Remove(roof);
                            model.Map.areaManager.NoRoof[roof] = false;
                        }
                    }

                    model.BuildingsToReinstall.Remove(building);

                    break;
                }
            }
        }

        /// <summary>
        /// Add building to cache when a building is removed and being in transport/reinstallation by a pawn.
        /// </summary>
        /// <param name="building"> Building under transportation or reinstallation. </param>
        public static void AddBeingReinstalledBuilding(Building building)
        {
            if (building == null)
                return;

            foreach (RemoveRoofModel model in _removeRoofModels)
            {
                if (model.BuildingsToReinstall.Contains(building))
                {
                    model.BuildingsBeingReinstalled.Add(building);
                    break;
                }
            }
        }

        /// <summary>
        /// Get a list of building being reinstalled that are in the same designation group as <paramref name="building"/>.
        /// </summary>
        /// <param name="building"> Building in question. </param>
        /// <returns> A list of buildings that are being reinstalled. </returns>
        public static HashSet<Building> GetBuildingsBeingReinstalled(Building building)
        {
            if (building == null)
                return new HashSet<Building>();

            foreach (RemoveRoofModel model in _removeRoofModels)
            {
                if (model.BuildingsToReinstall.Contains(building))
                {
                    return model.BuildingsBeingReinstalled;
                }
            }

            return new HashSet<Building>();
        }

        /// <summary>
        /// Remove building from cache.
        /// </summary>
        /// <param name="building"> Building in question. </param>
        public static void RemoveBuildingFromCache(Building building)
        {
            if (building == null)
                return;

            foreach (RemoveRoofModel model in _removeRoofModels)
            {
                if (model.BuildingsToReinstall.Contains(building))
                {
                    model.BuildingsBeingReinstalled.Remove(building);
                    model.BuildingsToReinstall.Remove(building);
                    if (!model.BuildingsToReinstall.Any() && !model.RoofToRemove.Any())
                    {
                        _removeRoofModels.Remove(model);
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// This method is invoked when at least one thing is selected by this selector.
        /// </summary>
        protected override void FinalizeDesignationSucceeded()
        {
            IncludeConduitsFromSelectedCells();

            if (SelectionHasThickRoof() && !HomeMoverMod.Setting.allowThickRoofMoves)
            {
                Messages.Message(UIText.ThickRoofBlocked.Translate(), MessageTypeDefOf.RejectInput);
                this.KeepDesignation = false;
                _mode = Mode.Select;
                Find.DesignatorManager.Deselect();
                return;
            }

            _mode = Mode.Place;
        }

        /// <summary>
        /// This method is invoked if this selector is selected.
        /// </summary>
        public override void Selected()
        {
            _mode = Mode.Select;
            _originFound = false;
            _origin = IntVec3.Zero;
            _rotation = Rot4.North;
            DesignatedThings.Clear();
            _selectedCells = new HashSet<IntVec3>();
            _ghostPos = new Dictionary<Thing, IntVec3>();
            _floorPattern = new Dictionary<IntVec3, TerrainDef>();
            _nonMinifiableThings = new HashSet<Thing>();
            this.KeepDesignation = false;
        }

        /// <summary>
        /// Rotation control. Code copied from vanilla.
        /// </summary>
        /// <param name="leftX"> X position on screen. </param>
        /// <param name="bottomY"> Bottom Y position on screen. </param>
        public override void DoExtraGuiControls(float leftX, float bottomY)
        {
            Rect winRect = new Rect(leftX, bottomY - 90f, 200f, 90f);
            Find.WindowStack.ImmediateWindow(
                RotationControlWindowId,
                winRect,
                WindowLayer.GameUI,
                delegate
                {
                    RotationDirection rotationDirection = RotationDirection.None;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Text.Font = GameFont.Medium;
                    Rect rect = new Rect(winRect.width / 2f - 64f - 5f, 15f, 64f, 64f);
                    if (Widgets.ButtonImage(rect, TexUI.RotLeftTex))
                    {
                        SoundDefOf.DragSlider.PlayOneShotOnCamera();
                        rotationDirection = RotationDirection.Counterclockwise;
                        Event.current.Use();
                    }
                    Widgets.Label(rect, KeyBindingDefOf.Designator_RotateLeft.MainKeyLabel);
                    Rect rect2 = new Rect(winRect.width / 2f + 5f, 15f, 64f, 64f);
                    if (Widgets.ButtonImage(rect2, TexUI.RotRightTex))
                    {
                        SoundDefOf.DragSlider.PlayOneShotOnCamera();
                        rotationDirection = RotationDirection.Clockwise;
                        Event.current.Use();
                    }
                    Widgets.Label(rect2, KeyBindingDefOf.Designator_RotateRight.MainKeyLabel);
                    if (rotationDirection != 0)
                    {
                        foreach (Thing thing in DesignatedThings)
                        {
                            _ghostPos[thing] = this.VectorRotation(
                                _ghostPos[thing],
                                rotationDirection,
                                thing
                            );
                        }
                        _rotation.Rotate(rotationDirection);
                    }
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                }
            );

            if (_mode == Mode.Place && DesignatedThings.Any())
            {
                Rect summaryRect = new Rect(leftX, bottomY - 220f, 380f, 120f);
                Find.WindowStack.ImmediateWindow(
                    RotationControlWindowId + 1,
                    summaryRect,
                    WindowLayer.GameUI,
                    delegate
                    {
                        Widgets.DrawMenuSection(summaryRect.AtZero());
                        Rect contentRect = summaryRect.AtZero().ContractedBy(8f);
                        int nonMinifiableCount = _nonMinifiableThings.Count;
                        var (obstructionCount, blockerSummary) = GetBlockerDetails(UI.MouseCell());
                        string summary =
                            UIText.OverlayItems.Translate(DesignatedThings.Count) + "\n"
                            + UIText.OverlayNonMinifiable.Translate(nonMinifiableCount) + "\n"
                            + UIText.OverlayObstructions.Translate(obstructionCount) + "\n"
                            + (obstructionCount > 0 ? "Blockers: " + blockerSummary + "\n" : "")
                            + (
                                HomeMoverMod.Setting.allowThickRoofMoves
                                    ? UIText.OverlayThickRoofOn.TranslateSimple()
                                    : UIText.OverlayThickRoofOff.TranslateSimple()
                            );
                        Widgets.Label(contentRect, summary);
                    }
                );
            }
        }

        /// <summary>
        /// What should be drawn when the frame is being updated..
        /// </summary>
        public override void SelectedUpdate()
        {
            GenDraw.DrawNoBuildEdgeLines();
            if (ArchitectCategoryTab.InfoRect.Contains(UI.MousePositionOnUIInverted))
                return;

            this.DrawGhostMatrix();

            if (_mode == Mode.Place && DesignatedThings.Any())
            {
                DrawBlockedCells(UI.MouseCell());
            }
        }

        /// <summary>
        /// Check if <paramref name="t"/> can be designated by this designator.
        /// </summary>
        /// <param name="t"> Thing in question. </param>
        /// <returns> Returns true is <paramref name="t"/> can be designated. </returns>
        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (_mode == Mode.Select)
            {
                Building building = t as Building;
                if (building == null)
                    return false;

                if (building.def.category != ThingCategory.Building)
                    return false;

                if (!DebugSettings.godMode && building.Faction != Faction.OfPlayer)
                {
                    if (building.Faction != null)
                        return false;

                    if (!building.ClaimableBy(Faction.OfPlayer))
                        return false;
                }

                if (base.Map.designationManager.DesignationOn(t, Designation) != null)
                    return false;

                if (
                    base.Map.designationManager.DesignationOn(t, DesignationDefOf.Deconstruct)
                    != null
                )
                    return false;

                return true;
            }

            return false;
        }

        /// <summary>
        /// This method is invoked when either a click or a drag from mouse is performed.
        /// </summary>
        /// <param name="loc"> cell on map. </param>
        /// <returns> Data model for acceptance. </returns>
        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            if (_mode == Mode.Select)
            {
                // Always accept the cell during Select if we want to capture floor terrain too —
                // floor-only cells (no building) must still be recorded for copyFloorTypes,
                // and they must register in _selectedCells so QueueDestinationRoof has the full footprint.
                if (HomeMoverMod.Setting.copyFloorTypes)
                    return true;

                return this.CanDesignateThing(this.TopReinstallableInCell(loc));
            }
            else
                return this.CanReinstallAllThings(loc);
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            if (_mode == Mode.Select)
            {
                List<Thing> things = this.ReinstallableInCell(c);
                if (things.Any())
                {
                    if (!_originFound)
                    {
                        _originFound = true;
                        _origin = c;
                    }

                    DesignatedThings.AddRange(things);
                    things.ForEach(thing => DesignateThing(thing));
                }

                _selectedCells.Add(c);

                // Establish origin from a floor-only cell if no building has been clicked yet.
                // Floor recording uses _origin, so without this floor patterns from cells before
                // the first building would all collapse to offset (0,0,0).
                if (!_originFound && HomeMoverMod.Setting.copyFloorTypes)
                {
                    _originFound = true;
                    _origin = c;
                }

                // Record floor terrain if enabled
                if (HomeMoverMod.Setting.copyFloorTypes && _originFound)
                {
                    TerrainDef terrain = c.GetTerrain(base.Map);
                    if (terrain != null && terrain.layerable)
                    {
                        IntVec3 offset = c - _origin;
                        if (!_floorPattern.ContainsKey(offset))
                        {
                            _floorPattern[offset] = terrain;
                            HomeMoverMod.DebugLog($"Recorded floor: {terrain.defName} at offset {offset}");
                        }
                    }
                }
            }
            else if (_mode == Mode.Place)
            {
                GenConstruct_CanPlaceBlueprintAt_Patch.Mode = BlueprintMode.Place;
                HashSet<Thing> placedThings = new HashSet<Thing>();
                Dictionary<Thing, Thing> twinThings = new Dictionary<Thing, Thing>();
                Dictionary<Thing, Blueprint_Install> blueprintWork =
                    new Dictionary<Thing, Blueprint_Install>();
                Dictionary<Thing, IntVec3> siblingWork = new Dictionary<Thing, IntVec3>();
                List<(Thing thing, string reason)> skippedItems = new List<(Thing, string)>();

                IntVec3 mousePos = UI.MouseCell();
                int floorsPlaced = 0;
                bool floorsQueued = false;

                List<Thing> orderedThings = HomeMoverMod.Setting.smartDependencyPlacement
                    ? DesignatedThings
                        .OrderBy(t => t.GetPlacementTier())
                        .ThenByDescending(t => t.def.altitudeLayer)
                        .ToList()
                    : DesignatedThings.ToList();

                foreach (Thing designatedThing in orderedThings)
                {
                    if (!floorsQueued && designatedThing.GetPlacementTier() != PlacementTier.Conduit)
                    {
                        floorsPlaced += MoveFloors(mousePos);
                        floorsQueued = true;
                    }

                    IntVec3 drawCell = GetDeltaCell(designatedThing, mousePos, _ghostPos);
                    List<Thing> things = drawCell.GetThingList(designatedThing.MapHeld);
                    bool foundTwin = false;
                    bool foundSibling = false;
                    foreach (Thing thingOnCell in things)
                    {
                        if (DesignatedThings.Contains(thingOnCell))
                        {
                            if (designatedThing.IdenticalWith(_rotation, thingOnCell))
                            {
                                if (
                                    blueprintWork.TryGetValue(
                                        thingOnCell,
                                        out Blueprint_Install install
                                    )
                                )
                                {
                                    Thing twin = this.GetTailInTwinThings(
                                        twinThings,
                                        designatedThing
                                    );

                                    _setBuildingToReinstall.Invoke(install, new[] { twin });
                                    blueprintWork[twin] = install;
                                    blueprintWork.Remove(thingOnCell);
                                    placedThings.Add(twin);
                                }
                                else if (siblingWork.TryGetValue(thingOnCell, out IntVec3 position))
                                {
                                    Thing twin = this.GetTailInTwinThings(
                                        twinThings,
                                        designatedThing
                                    );
                                    _ghostPos[twin] = position;
                                    siblingWork.Remove(thingOnCell);
                                    siblingWork[twin] = position;
                                    placedThings.Add(thingOnCell);
                                }
                                else
                                {
                                    twinThings[thingOnCell] = designatedThing;
                                    _ghostPos[designatedThing] = _ghostPos[thingOnCell];
                                    placedThings.Add(thingOnCell);
                                }

                                this.Map.designationManager.TryRemoveDesignationOn(
                                    thingOnCell,
                                    HomeMoverDefOf.HomeMover
                                );

                                foundTwin = true;
                                break;
                            }
                            else if (!GenConstruct.BlocksConstruction(designatedThing, thingOnCell))
                            {
                                continue;
                            }
                            else
                            {
                                foundSibling = true;
                                Thing twin = this.GetTailInTwinThings(twinThings, designatedThing);
                                siblingWork[twin] = _ghostPos[twin] = _ghostPos[designatedThing];
                                break;
                            }
                        }
                    }

                    if (foundTwin || foundSibling)
                        continue;

                    Thing twin1 = this.GetTailInTwinThings(twinThings, designatedThing);

                    AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(
                        twin1.def,
                        drawCell,
                        GetRotation(twin1),
                        twin1.MapHeld,
                        false,
                        null,
                        twin1
                    );

                    if (report.Accepted)
                    {
                        Building building = twin1 as Building;

                        if (building == null)
                        {
                            skippedItems.Add((twin1, UIText.ItemNotMinifiable.Translate(twin1.LabelCap)));
                            continue;
                        }

                        if (HomeMoverMod.Setting.queueDestinationRoof && building.def.holdsRoof)
                        {
                            QueueDestinationRoof(building, drawCell, building.MapHeld);
                        }

                        blueprintWork[building] = GenConstruct.PlaceBlueprintForReinstall(
                            building,
                            drawCell,
                            building.MapHeld,
                            GetRotation(building),
                            Faction.OfPlayer
                        );
                        placedThings.Add(building);
                        HomeMoverMod.DebugLog($"Placed {building.LabelCap} successfully");
                    }
                    else if (HomeMoverMod.Setting.skipItemsWithErrors)
                    {
                        // Skip this item and track it
                        skippedItems.Add((twin1, report.Reason));
                        HomeMoverMod.DebugLog($"Skipping {twin1.LabelCap}: {report.Reason}");
                    }
                    else
                    {
                        // Old behavior: abort entire operation
                        Messages.Message(
                            UIText.PlacementError.Translate(twin1.LabelCap, report.Reason),
                            MessageTypeDefOf.RejectInput
                        );
                        this.KeepDesignation = true;
                        _mode = Mode.Select;
                        Find.DesignatorManager.Deselect();
                        return;
                    }
                }

                if (!floorsQueued)
                {
                    floorsPlaced += MoveFloors(mousePos);
                    floorsQueued = true;
                }

                if (floorsPlaced > 0)
                {
                    Messages.Message(
                        UIText.FloorsQueued.Translate(floorsPlaced),
                        MessageTypeDefOf.TaskCompletion
                    );
                }

                // Show message for skipped items if any
                if (skippedItems.Any() && HomeMoverMod.Setting.showSkippedItemsMessage)
                {
                    string itemList = string.Join("\n", skippedItems.Select(x =>
                        $"- {x.thing.LabelCap}: {x.reason}"
                    ));
                    Messages.Message(
                        UIText.ItemsSkipped.Translate(itemList),
                        MessageTypeDefOf.CautionInput
                    );
                }

                RemoveRoofModel model = InitModel(
                    DesignatedThings,
                    DesignatedThings.Except(placedThings).ToList(),
                    DesignatedThings.First().MapHeld,
                    mousePos,
                    _rotation,
                    _ghostPos
                );

                ResolveDeadLock(model);

                this.KeepDesignation = true;
                _mode = Mode.Select;
                Find.DesignatorManager.Deselect();
            }
        }

        /// <summary>
        /// In Place mode, only act on the final cell of a drag to prevent ghost blueprints.
        /// </summary>
        public override void DesignateMultiCell(IEnumerable<IntVec3> cells)
        {
            if (_mode == Mode.Place)
            {
                IntVec3 last = IntVec3.Invalid;
                foreach (IntVec3 c in cells)
                    last = c;
                if (last.IsValid)
                    DesignateSingleCell(last);
            }
            else
            {
                base.DesignateMultiCell(cells);
            }
        }

        public override void DesignateThing(Thing t)
        {
            if (t == null)
                return;

            if (t.Faction != Faction.OfPlayer)
                t.SetFaction(Faction.OfPlayer);

            if (t is Building building && !building.def.Minifiable)
                _nonMinifiableThings.Add(t);

            base.Map.designationManager.AddDesignation(new Designation(t, this.Designation));
            _ghostPos[t] = t.Position - _origin;
        }

        public override void DrawMouseAttachments()
        {
            if (_mode == Mode.Select)
                base.DrawMouseAttachments();
            else
                this.DrawGhostMatrix();
        }

        public override void SelectedProcessInput(Event ev)
        {
            base.SelectedProcessInput(ev);
            if (DesignatedThings.Any() && _mode == Mode.Place)
            {
                HandleRotationShortcuts();
            }
        }

        /// <summary>
        /// Set no roof to false after roof is removed on <paramref name="cell"/>.
        /// </summary>
        /// <param name="cell"></param>
        /// <remarks> When no-roof is true, pawn will try to remove roof on that cell. </remarks>
        public static void SetNoRoofFalse(IntVec3 cell)
        {
            foreach (RemoveRoofModel model in _removeRoofModels)
            {
                if (model.RoofToRemove?.Contains(cell) ?? false)
                {
                    model.Map.areaManager.NoRoof[cell] = false;
                    model.RoofToRemove.Remove(cell);
                    if (!model.BuildingsToReinstall.Any() && !model.RoofToRemove.Any())
                    {
                        _removeRoofModels.Remove(model);
                    }

                    break;
                }
            }
        }

        public static void AddToRoofToRemove(IntVec3 roof, Thing thing)
        {
            if (
                HomeMoverMod.Setting.allowThickRoofMoves
                && thing?.MapHeld != null
                && roof.GetRoof(thing.MapHeld)?.isThickRoof == true
            )
            {
                return;
            }

            _removeRoofModels
                .FirstOrDefault(model => model.BuildingsToReinstall.Contains(thing))
                ?.RoofToRemove.Add(roof);
        }

        public static void UninstallJobCallback(Building building, Map map)
        {
            foreach (RemoveRoofModel model in _removeRoofModels)
            {
                if (model.WaitingThings.Contains(building))
                {
                    MinifiedThing minifiedThing = (MinifiedThing)
                        map
                            .listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing)
                            .FirstOrDefault(t => t.GetInnerIfMinified() == building);
                    model.GhostPosition[minifiedThing] = model.GhostPosition[building];
                    model.WaitingThings.Remove(building);
                    model.WaitingThings.Add(minifiedThing);
                }
            }
        }

        private static void ResolveDeadLock(RemoveRoofModel model)
        {
            foreach (Thing thing in model.WaitingThings.ToList())
            {
                Thing lastFound = thing;
                List<Thing> foundThings = new List<Thing>() { lastFound };

                while (true)
                {
                    IntVec3 spawnCell = GetDeltaCell(
                        lastFound,
                        model.MousePos,
                        model.GhostPosition
                    );
                    List<IntVec3> rect = GenAdj
                        .OccupiedRect(
                            spawnCell,
                            lastFound.Rotate(model.Rotation),
                            lastFound.def.size
                        )
                        .ToList();
                    if (lastFound.def.hasInteractionCell)
                    {
                        rect.Add(
                            Verse.ThingUtility.InteractionCellWhenAt(
                                lastFound.def,
                                spawnCell,
                                lastFound.Rotate(model.Rotation),
                                lastFound.MapHeld
                            )
                        );
                    }
                    IEnumerable<Thing> thingsOnCell = rect.SelectMany(c =>
                        c.GetThingList(lastFound.MapHeld)
                            .Where(t => t.def.blueprintDef != null && t.def.Minifiable)
                    );
                    lastFound =
                        thingsOnCell.FirstOrDefault(t =>
                            GenConstruct.BlocksConstruction(lastFound, t)
                            && model.WaitingThings.Contains(t)
                        )
                        ?? ThingUtility.BlockAdjacentInteractionCell(
                            lastFound,
                            spawnCell,
                            lastFound.Rotate(model.Rotation)
                        );
                    if (lastFound == null || foundThings.Contains(lastFound))
                        break;

                    foundThings.Add(lastFound);
                }

                if (lastFound == null)
                {
                    continue;
                }
                else
                {
                    thing.Map.designationManager.AddDesignation(
                        new Designation(thing, DesignationDefOf.Uninstall)
                    );
                }
            }
        }

        private Thing GetTailInTwinThings(Dictionary<Thing, Thing> table, Thing thing)
        {
            while (table.TryGetValue(thing, out Thing tail))
                thing = tail;

            return thing;
        }

        private static IntVec3 GetDeltaCell(
            Thing thing,
            IntVec3 mousePos,
            Dictionary<Thing, IntVec3> ghostPos
        )
        {
            return new IntVec3(
                mousePos.x + ghostPos[thing].x,
                mousePos.y,
                mousePos.z + ghostPos[thing].z
            );
        }

        private static void RemoveEmptyCache()
        {
            foreach (RemoveRoofModel model in _removeRoofModels.ToList())
            {
                if (
                    model.BuildingsToReinstall.EnumerableNullOrEmpty()
                    && model.RoofToRemove.EnumerableNullOrEmpty()
                )
                {
                    _removeRoofModels.Remove(model);
                }
            }
        }

        private List<Thing> ReinstallableInCell(IntVec3 loc)
        {
            List<Thing> things = new List<Thing>();

            foreach (
                Thing item in from t in base.Map.thingGrid.ThingsAt(loc)
                              orderby t.def.altitudeLayer descending
                              select t
            )
            {
                if (this.CanDesignateThing(item).Accepted)
                    things.Add(item);
            }

            return things;
        }

        private Thing TopReinstallableInCell(IntVec3 loc)
        {
            foreach (
                Thing item in from t in base.Map.thingGrid.ThingsAt(loc)
                              orderby t.def.altitudeLayer descending
                              select t
            )
            {
                if (this.CanDesignateThing(item).Accepted)
                {
                    return item;
                }
            }

            return null;
        }

        private AcceptanceReport CanReinstallAllThings(IntVec3 mousePos)
        {
            // If obstruction handling is enabled, be lenient — let placement proceed and handle blockers during actual placement
            if (HomeMoverMod.Setting.handleObstructions)
                return AcceptanceReport.WasAccepted;

            AcceptanceReport result = AcceptanceReport.WasAccepted;
            GenConstruct_CanPlaceBlueprintAt_Patch.Mode = BlueprintMode.Check;
            this.TraverseDesignateThings(
                mousePos,
                (drawCell, thing) =>
                {
                    AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(
                        thing.def,
                        drawCell,
                        GetRotation(thing),
                        thing.MapHeld,
                        false,
                        null,
                        thing
                    );
                    if (!report.Accepted)
                    {
                        if (_nonMinifiableThings.Contains(thing))
                            return false;

                        result = report;
                        return true;
                    }

                    return false;
                }
            );

            return result;
        }

        private AcceptanceReport CanReinstall(Thing thing, IntVec3 drawCell)
        {
            if (_nonMinifiableThings.Contains(thing))
                return AcceptanceReport.WasAccepted;

            GenConstruct_CanPlaceBlueprintAt_Patch.Mode = BlueprintMode.Check;
            AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(
                thing.def,
                drawCell,
                GetRotation(thing),
                thing.MapHeld,
                false,
                null,
                thing
            );

            return report;
        }

        private static Rot4 GetRotation(Thing thing)
        {
            if (thing.def.rotatable)
                return new Rot4(_rotation.AsInt + thing.Rotation.AsInt);
            else
                return thing.Rotation;
        }

        private static void QueueDestinationRoof(Building sourceBuilding, IntVec3 destination, Map map)
        {
            if (sourceBuilding?.MapHeld == null || map == null)
                return;

            // Translate the entire selection footprint by the move offset so we
            // only mark BuildRoof for cells inside the moved region — never beyond it.
            IntVec3 moveOffset = destination - sourceBuilding.Position;

            foreach (IntVec3 selectedCell in _selectedCells)
            {
                RoofDef sourceRoofDef = selectedCell.GetRoof(sourceBuilding.MapHeld);
                if (sourceRoofDef == null)
                    continue;

                if (sourceRoofDef.isThickRoof && HomeMoverMod.Setting.allowThickRoofMoves)
                    continue;

                IntVec3 targetRoof = selectedCell + moveOffset;
                if (!targetRoof.InBounds(map))
                    continue;

                map.areaManager.BuildRoof[targetRoof] = true;
            }
        }

        private void IncludeConduitsFromSelectedCells()
        {
            if (!_selectedCells.Any())
                return;

            foreach (IntVec3 cell in _selectedCells)
            {
                foreach (Thing thing in cell.GetThingList(base.Map))
                {
                    if (
                        thing is Building
                        && thing.def.IsConduitLike()
                        && !DesignatedThings.Contains(thing)
                        && this.CanDesignateThing(thing).Accepted
                    )
                    {
                        DesignatedThings.Add(thing);
                        DesignateThing(thing);
                    }
                }
            }
        }

        private bool SelectionHasThickRoof()
        {
            foreach (Thing thing in DesignatedThings)
            {
                if (!(thing is Building building) || !building.def.holdsRoof)
                    continue;

                foreach (IntVec3 cell in building.OccupiedRect())
                {
                    RoofDef roof = cell.GetRoof(base.Map);
                    if (roof?.isThickRoof == true)
                        return true;
                }
            }

            return false;
        }

        private int MoveFloors(IntVec3 mousePos)
        {
            if (!HomeMoverMod.Setting.copyFloorTypes || !_floorPattern.Any())
                return 0;

            int floorsPlaced = 0;
            Map map = base.Map;

            foreach (var kvp in _floorPattern)
            {
                IntVec3 offset = kvp.Key;
                TerrainDef terrainToBuild = kvp.Value;

                IntVec3 floorPos = mousePos + offset;

                if (!floorPos.InBounds(map))
                    continue;

                if (terrainToBuild.blueprintDef == null)
                    continue;

                TerrainDef currentTerrain = floorPos.GetTerrain(map);
                if (currentTerrain == terrainToBuild)
                    continue;

                // Skip if there's already a build blueprint here
                bool hasBlueprint = floorPos.GetThingList(map).Any(t => t is Blueprint_Build);
                if (hasBlueprint)
                    continue;

                // Use vanilla's own placement check — it correctly allows floors under beds/tables/etc.
                AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(
                    terrainToBuild,
                    floorPos,
                    Rot4.North,
                    map
                );

                if (!report.Accepted)
                {
                    HomeMoverMod.DebugLog($"Skipping floor {terrainToBuild.defName} at {floorPos}: {report.Reason}");
                    continue;
                }

                Blueprint_Build bp = GenConstruct.PlaceBlueprintForBuild(
                    terrainToBuild,
                    floorPos,
                    map,
                    Rot4.North,
                    Faction.OfPlayer,
                    GetDefaultStuffFor(terrainToBuild)
                );

                if (bp != null)
                {
                    floorsPlaced++;
                    HomeMoverMod.DebugLog($"Placed floor blueprint {terrainToBuild.defName} at {floorPos}");
                }
            }

            return floorsPlaced;
        }

        /// <summary>
        /// Picks a default stuff ThingDef for a terrain that requires Stuff (e.g. CarpetRed → cloth).
        /// Without this, PlaceBlueprintForBuild creates an invalid blueprint that's silently destroyed.
        /// </summary>
        private static ThingDef GetDefaultStuffFor(BuildableDef def)
        {
            if (def == null || !def.MadeFromStuff)
                return null;

            // Prefer the def's own default if available
            ThingDef fallback = GenStuff.DefaultStuffFor(def);
            if (fallback != null)
                return fallback;

            // Otherwise grab any allowed stuff
            if (def.stuffCategories != null)
            {
                foreach (var cat in def.stuffCategories)
                {
                    var stuff = DefDatabase<ThingDef>.AllDefsListForReading
                        .FirstOrDefault(t => t.IsStuff && t.stuffProps?.categories != null && t.stuffProps.categories.Contains(cat));
                    if (stuff != null)
                        return stuff;
                }
            }

            return null;
        }

        private (int count, string summary) GetBlockerDetails(IntVec3 mousePos)
        {
            HashSet<int> blockerIds = new HashSet<int>();
            Dictionary<string, int> blockerTypes = new Dictionary<string, int>();
            HashSet<Thing> moveGroup = DesignatedThings.ToHashSet();

            foreach (Thing thing in DesignatedThings)
            {
                Thing inner = thing.GetInnerIfMinified();
                IntVec3 drawCell = GetDeltaCell(thing, mousePos, _ghostPos);

                foreach (IntVec3 cell in GenAdj.OccupiedRect(drawCell, GetRotation(thing), inner.def.Size))
                {
                    foreach (Thing blocker in cell.GetThingList(base.Map))
                    {
                        if (blocker == null || blocker.DestroyedOrNull())
                            continue;

                        if (moveGroup.Contains(blocker))
                            continue;

                        if (!GenConstruct.BlocksConstruction(inner, blocker))
                            continue;

                        int id = blocker.thingIDNumber;
                        if (blockerIds.Add(id))
                        {
                            string typeKey = "Other";
                            if (blocker is Plant)
                                typeKey = "Plant";
                            else if (blocker.def.mineable)
                                typeKey = "Rock";
                            else if (blocker is Building)
                                typeKey = "Building";

                            if (!blockerTypes.ContainsKey(typeKey))
                                blockerTypes[typeKey] = 0;
                            blockerTypes[typeKey]++;
                        }
                    }
                }
            }

            string summary = string.Join(", ", blockerTypes.Select(kvp => $"{kvp.Key}({kvp.Value})"));
            return (blockerIds.Count, summary);
        }

        private void DrawBlockedCells(IntVec3 mousePos)
        {
            HashSet<int> blockerIds = new HashSet<int>();
            HashSet<Thing> moveGroup = DesignatedThings.ToHashSet();

            foreach (Thing thing in DesignatedThings)
            {
                Thing inner = thing.GetInnerIfMinified();
                IntVec3 drawCell = GetDeltaCell(thing, mousePos, _ghostPos);

                foreach (IntVec3 cell in GenAdj.OccupiedRect(drawCell, GetRotation(thing), inner.def.Size))
                {
                    foreach (Thing blocker in cell.GetThingList(base.Map))
                    {
                        if (blocker == null || blocker.DestroyedOrNull())
                            continue;

                        if (moveGroup.Contains(blocker))
                            continue;

                        if (!GenConstruct.BlocksConstruction(inner, blocker))
                            continue;

                        int id = blocker.thingIDNumber;
                        if (blockerIds.Add(id))
                        {
                            GenDraw.DrawFieldEdges(GenAdj.OccupiedRect(cell, Rot4.North, new IntVec2(1, 1)).ToList(), Color.red);
                        }
                    }
                }
            }
        }

        private void DrawGhostMatrix()
        {
            IntVec3 mousePos = UI.MouseCell();
            this.TraverseDesignateThings(
                mousePos,
                (drawCell, thing) =>
                {
                    this.DrawGhostThing(drawCell, thing);
                    return false;
                }
            );
        }

        private void TraverseDesignateThings(IntVec3 mousePos, Func<IntVec3, Thing, bool> func)
        {
            foreach (Thing thing in DesignatedThings)
            {
                if (func(GetDeltaCell(thing, mousePos, _ghostPos), thing))
                    break;
            }
        }

        private void DrawGhostThing(IntVec3 cell, Thing thing)
        {
            Graphic baseGraphic = thing.Graphic.ExtractInnerGraphicFor(thing);
            Color color = this.CanReinstall(thing, cell).Accepted
                ? Designator_Place.CanPlaceColor
                : Designator_Place.CannotPlaceColor;
            try
            {
                GhostDrawer.DrawGhostThing(
                    cell,
                    GetRotation(thing),
                    thing.def,
                    baseGraphic,
                    color,
                    AltitudeLayer.Blueprint,
                    thing
                );
            }
            catch
            {
                // no op.
            }
        }

        private IntVec3 VectorRotation(
            IntVec3 cell,
            RotationDirection rotationDirection,
            Thing thing
        )
        {
            if (thing.def.rotatable || thing.def.size == IntVec2.One)
            {
                return Rotate(cell);
            }
            else
            {
                HomeMoverMod.DebugLog($"Position: {cell}");
                IEnumerable<IntVec3> corners = thing
                    .OccupiedRect()
                    .Corners.Select(corner =>
                    {
                        IntVec3 normalized = corner - thing.Position + cell;
                        HomeMoverMod.DebugLog($"Normalized: {normalized}");
                        return normalized;
                    })
                    .Select(Rotate);

                var value = GetCenter(corners.ToList());
                HomeMoverMod.DebugLog($"Return value: {value}");
                return value;
            }

            IntVec3 Rotate(IntVec3 pos)
            {
                switch (rotationDirection)
                {
                    case RotationDirection.Clockwise:
                        return new IntVec3(pos.z, pos.y, -pos.x);
                    case RotationDirection.Counterclockwise:
                        return new IntVec3(-pos.z, pos.y, pos.x);
                    default:
                        return pos;
                }
            }

            IntVec3 GetCenter(List<IntVec3> cells)
            {
                int minX,
                    minZ,
                    maxX,
                    maxZ;
                maxX = maxZ = int.MinValue;
                minX = minZ = int.MaxValue;

                foreach (IntVec3 c in cells)
                {
                    HomeMoverMod.DebugLog($"Rotated: {c}");
                    minX = c.x < minX ? c.x : minX;
                    minZ = c.z < minZ ? c.z : minZ;
                    maxX = c.x > maxX ? c.x : maxX;
                    maxZ = c.z > maxZ ? c.z : maxZ;
                }

                return new IntVec3((maxX - minX) / 2 + minX, cells[0].y, (maxZ - minZ) / 2 + minZ);
            }
        }

        private void HandleRotationShortcuts()
        {
            RotationDirection rotationDirection = RotationDirection.None;
            if (KeyBindingDefOf.Designator_RotateRight.KeyDownEvent)
            {
                rotationDirection = RotationDirection.Clockwise;
            }
            else if (KeyBindingDefOf.Designator_RotateLeft.KeyDownEvent)
            {
                rotationDirection = RotationDirection.Counterclockwise;
            }

            if (rotationDirection != RotationDirection.None)
            {
                foreach (Thing thing in DesignatedThings)
                {
                    _ghostPos[thing] = this.VectorRotation(
                        _ghostPos[thing],
                        rotationDirection,
                        thing
                    );
                }
                _rotation.Rotate(rotationDirection);
                SoundDefOf.DragSlider.PlayOneShotOnCamera();
            }
        }

        private static RemoveRoofModel InitModel(
            List<Thing> designatedThings,
            List<Thing> waitingThings,
            Map map,
            IntVec3 mousePos,
            Rot4 rotation,
            Dictionary<Thing, IntVec3> ghostPos
        )
        {
            RemoveRoofModel newModel = new RemoveRoofModel(
                designatedThings,
                designatedThings
                    .OfType<Building>()
                    .Where(b => b.def.holdsRoof)
                    .Where(
                        b =>
                            !HomeMoverMod.Setting.allowThickRoofMoves
                            || !b.OccupiedRect()
                                .Any(c => c.GetRoof(map)?.isThickRoof == true)
                    )
                    .ToHashSet(),
                waitingThings,
                new HashSet<IntVec3>(),
                map,
                mousePos,
                rotation,
                ghostPos
            );
            _removeRoofModels.Add(newModel);
            return newModel;
        }

        private class RemoveRoofModel : IExposable
        {
            private List<Thing> ghostThings = new List<Thing>();
            private List<IntVec3> ghostPos = new List<IntVec3>();

            public HashSet<Building> BuildingsToReinstall = new HashSet<Building>();

            public HashSet<Building> BuildingsBeingReinstalled = new HashSet<Building>();

            public List<Thing> DesignatedThings = new List<Thing>();

            public HashSet<Thing> WaitingThings = new HashSet<Thing>();

            public HashSet<IntVec3> RoofToRemove = new HashSet<IntVec3>();

            public Dictionary<Thing, IntVec3> GhostPosition = new Dictionary<Thing, IntVec3>();

            public IntVec3 MousePos;

            public Rot4 Rotation;

            public Map Map;

            public RemoveRoofModel() { }

            public RemoveRoofModel(
                List<Thing> designatedThings,
                HashSet<Building> roofSupporterToReinstall,
                IEnumerable<Thing> waitingThings,
                HashSet<IntVec3> roofToRemove,
                Map map,
                IntVec3 mousePos,
                Rot4 rotation,
                Dictionary<Thing, IntVec3> ghostPos
            )
            {
                this.DesignatedThings = designatedThings;
                this.BuildingsToReinstall = roofSupporterToReinstall;
                this.RoofToRemove = roofToRemove;
                this.Map = map;
                this.BuildingsBeingReinstalled = new HashSet<Building>();
                this.WaitingThings = waitingThings.ToHashSet();
                this.MousePos = mousePos;
                this.Rotation = rotation;
                this.GhostPosition = ghostPos;
            }

            public void ExposeData()
            {
                if (Scribe.mode == LoadSaveMode.Saving)
                {
                    this.CleanCache();
                }

                Scribe_Collections.Look(
                    ref this.BuildingsToReinstall,
                    nameof(this.BuildingsToReinstall),
                    LookMode.Reference
                );
                Scribe_Collections.Look(
                    ref this.BuildingsBeingReinstalled,
                    nameof(this.BuildingsBeingReinstalled),
                    LookMode.Reference
                );
                Scribe_Collections.Look(
                    ref this.RoofToRemove,
                    nameof(RoofToRemove),
                    LookMode.Value
                );
                Scribe_Collections.Look(
                    ref this.DesignatedThings,
                    nameof(this.DesignatedThings),
                    LookMode.Reference
                );
                Scribe_Collections.Look(
                    ref this.WaitingThings,
                    nameof(this.WaitingThings),
                    LookMode.Reference
                );
                Scribe_References.Look(ref this.Map, nameof(this.Map));
                Scribe_Values.Look(ref this.MousePos, nameof(this.MousePos));
                Scribe_Values.Look(ref this.Rotation, nameof(this.Rotation));

                Scribe_Collections.Look(
                    ref this.GhostPosition,
                    nameof(this.GhostPosition),
                    LookMode.Reference,
                    LookMode.Value,
                    ref ghostThings,
                    ref ghostPos
                );
            }

            private void CleanCache()
            {
                RemoveDestroyedThings(this.DesignatedThings);
                RemoveDestroyedThings(this.BuildingsBeingReinstalled);
                RemoveDestroyedThings(this.BuildingsToReinstall);
                RemoveDestroyedThings(this.WaitingThings);

                if (this.GhostPosition is null)
                    return;

                foreach (Thing key in this.GhostPosition.Keys.ToList())
                {
                    if (key.Destroyed)
                    {
                        this.GhostPosition.Remove(key);
                    }
                }
            }

            private static void RemoveDestroyedThings<T>(ICollection<T> things)
                where T : Thing
            {
                if (things.EnumerableNullOrEmpty())
                    return;

                foreach (T thing in new List<T>(things))
                {
                    if (thing.Destroyed)
                    {
                        things.Remove(thing);
                    }
                }
            }
        }
    }
}
