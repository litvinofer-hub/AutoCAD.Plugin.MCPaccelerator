using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin
{
    /// <summary>
    /// Represents one floor plan's working area in the AutoCAD drawing.
    /// Holds every AutoCAD ObjectId the user selected (or that was found
    /// inside the boundary during refresh), plus the bounding-box frame
    /// and label that were drawn around them.
    ///
    /// Also stores the ID mapping between AutoCAD source polylines and the
    /// domain Building elements that were derived from them.
    /// Lives in AutoCADPlugin so it may reference AutoCAD types freely.
    ///
    /// Mutable: <see cref="SelectedObjectIds"/>, <see cref="FrameId"/>,
    /// and <see cref="LabelId"/> can be updated during refresh or when
    /// the axial system changes the boundary size.
    /// </summary>
    public class FloorPlanWorkingArea
    {
        public Guid BuildingId { get; }
        public Guid StoryId { get; }
        public string BuildingName { get; }
        public string StoryName { get; }

        /// <summary>
        /// All entity ObjectIds currently inside this working area.
        /// Updated during refresh or when elements (e.g. axial system)
        /// are added/removed.
        /// </summary>
        public List<ObjectId> SelectedObjectIds { get; set; }

        /// <summary>ObjectId of the bounding-box polyline drawn on the frame layer.</summary>
        public ObjectId FrameId { get; set; }

        /// <summary>ObjectId of the label DBText at the bottom-left of the frame.</summary>
        public ObjectId LabelId { get; set; }

        /// <summary>
        /// Maps a domain element Guid (Wall / Window / Door) to the AutoCAD
        /// ObjectId(s) it was created from. This is the bridge between the
        /// pure domain model and the AutoCAD drawing.
        /// </summary>
        private readonly Dictionary<Guid, List<ObjectId>> _domainToAcad = [];
        public IReadOnlyDictionary<Guid, List<ObjectId>> DomainToAcadMap => _domainToAcad;

        public FloorPlanWorkingArea(
            Guid buildingId, Guid storyId,
            string buildingName, string storyName,
            List<ObjectId> selectedObjectIds,
            ObjectId frameId, ObjectId labelId)
        {
            BuildingId = buildingId;
            StoryId = storyId;
            BuildingName = buildingName;
            StoryName = storyName;
            SelectedObjectIds = selectedObjectIds;
            FrameId = frameId;
            LabelId = labelId;
        }

        /// <summary>
        /// Clears all domain-to-AutoCAD mappings. Called during refresh
        /// before domain elements are re-created.
        /// </summary>
        public void ClearDomainMap() => _domainToAcad.Clear();

        /// <summary>
        /// Registers a link from a domain element to one source AutoCAD entity.
        /// </summary>
        public void MapDomainElement(Guid domainElementId, ObjectId sourceObjectId)
        {
            if (!_domainToAcad.TryGetValue(domainElementId, out var list))
            {
                list = new List<ObjectId>();
                _domainToAcad[domainElementId] = list;
            }
            list.Add(sourceObjectId);
        }

        /// <summary>
        /// Registers a link from a domain element to multiple source entities.
        /// </summary>
        public void MapDomainElement(Guid domainElementId, IEnumerable<ObjectId> sourceObjectIds)
        {
            foreach (var id in sourceObjectIds)
                MapDomainElement(domainElementId, id);
        }
    }
}
