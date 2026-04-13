using System;
using System.Collections.Generic;
using System.Linq;

namespace MCPAccelerator.AutoCAD.AutoCADPlugin
{
    /// <summary>
    /// Collects all <see cref="FloorPlanWorkingArea"/> instances that belong to
    /// a single building. Each entry represents one story's floor plan.
    /// </summary>
    public class FloorPlanWorkingAreas
    {
        private readonly List<FloorPlanWorkingArea> _areas = [];

        public IReadOnlyList<FloorPlanWorkingArea> Areas => _areas.AsReadOnly();

        public void Add(FloorPlanWorkingArea area) => _areas.Add(area);

        public FloorPlanWorkingArea FindByStory(Guid storyId)
        {
            return _areas.FirstOrDefault(a => a.StoryId == storyId);
        }

        public bool Remove(FloorPlanWorkingArea area) => _areas.Remove(area);

        public void Clear() => _areas.Clear();
    }
}
