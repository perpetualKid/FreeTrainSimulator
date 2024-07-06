using FreeTrainSimulator.Graphics.Xna;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

namespace Tests.FreeTrainSimulator.Graphics.Xna
{
    [TestClass]
    public class ResourceGameComponentTest
    {
        [TestMethod]
        public void ExistsReferenceTypeTest()
        {
            using (Game game = new Game())
            {
                using (ResourceGameComponent<string, int> resourceComponent = new ResourceGameComponent<string, int>(game))
                {
                    Assert.IsFalse(resourceComponent.Exists(12345));
                    resourceComponent.Get(123456, () => string.Empty);
                    Assert.IsTrue(resourceComponent.Exists(123456));
                }
            }
        }

        [TestMethod]
        public void ExistsValueTypeTest()
        {
            using (Game game = new Game())
            {
                using (ResourceGameComponent<int, int> resourceComponent = new ResourceGameComponent<int, int>(game))
                {
                    Assert.IsFalse(resourceComponent.Exists(12345));
                    resourceComponent.Get(123456, () => -1);
                    Assert.IsTrue(resourceComponent.Exists(123456));
                }
            }
        }

    }
}
