using MCPAccelerator.Utils.GeometryModel;
using Xunit;

namespace MCPAccelerator.Tests.GeometryModel
{
    public class GeometrySettingsTests
    {
        [Fact]
        public void Tolerance_Value_Is1e6()
        {
            Assert.Equal(1e-6, GeometrySettings.Tolerance);
        }
    }
}
