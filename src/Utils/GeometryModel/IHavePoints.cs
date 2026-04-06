using System.Collections.Generic;

namespace MCPAccelerator.Utils.GeometryModel
{
    public interface IHavePoints
    {
        IEnumerable<Point> GetPoints();
    }
}
