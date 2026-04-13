using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin
{
    /// <summary>
    /// Represents one floor plan's working area in the AutoCAD drawing.
    /// Holds every AutoCAD ObjectId the user selected, plus the bounding-box
    /// frame and label that were drawn around them.
    ///
    /// Also stores the ID mapping between AutoCAD source polylines and the
    /// domain Building elements that were derived from them.
    /// Lives in AutoCADPlugin so it may reference AutoCAD types freely.
    /// </summary>
    public class FloorPlanWorkingArea
    {
        public Guid BuildingId { get; }
        public Guid StoryId { get; }
        public string BuildingName { get; }
        public string StoryName { get; }

        /// <summary>All selected polyline ObjectIds (walls + windows + doors), before any filtering.</summary>
        public IReadOnlyList<ObjectId> SelectedObjectIds { get; }

        /// <summary>ObjectId of the bounding-box polyline drawn on the frame layer.</summary>
        public ObjectId FrameId { get; }

        /// <summary>ObjectId of the label DBText at the bottom-left of the frame.</summary>
        public ObjectId LabelId { get; }

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
            IReadOnlyList<ObjectId> selectedObjectIds,
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
        /// Registers a link from a domain element to one source AutoCAD polyline.
        /// </summary>
        public void MapDomainElement(Guid domainElementId, ObjectId sourcePolylineId)
        {
            if (!_domainToAcad.TryGetValue(domainElementId, out var list))
            {
                list = new List<ObjectId>();
                _domainToAcad[domainElementId] = list;
            }
            list.Add(sourcePolylineId);
        }

        /// <summary>
        /// Registers a link from a domain element to multiple source polylines.
        /// </summary>
        public void MapDomainElement(Guid domainElementId, IEnumerable<ObjectId> sourcePolylineIds)
        {
            foreach (var id in sourcePolylineIds)
                MapDomainElement(domainElementId, id);
        }
    }
}
