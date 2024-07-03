using FreeTrainSimulator.Common.Position;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common.Position
{
    [TestClass]
    public class TileHelperTests
    {
        [TestMethod]
        public void SnapTest()
        {
            int tileX = 23;
            int tileZ = 17;
            Tile tile = new Tile(tileX, tileZ);

            Tile result = TileHelper.Snap(tile, TileHelper.TileZoom.DistantMountainLarge);
        }
    }
}
