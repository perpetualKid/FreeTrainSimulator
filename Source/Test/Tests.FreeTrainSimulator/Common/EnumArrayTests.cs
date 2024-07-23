using System.IO;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;

using MemoryPack;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator.Common
{
    [TestClass]
    public class EnumArrayTests
    {

        [TestMethod]
        public async Task EnumArraySerializationRoundTripTest()
        {
            EnumArray<string, SeasonType> source = new EnumArray<string, SeasonType>();
            source[SeasonType.Summer] = "Hot";
            source[SeasonType.Winter] = "Cool";

            using (MemoryStream stream = new MemoryStream())
            {
                await MemoryPackSerializer.SerializeAsync(stream, source).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);

                stream.Position = 0;
                EnumArray<string, SeasonType> target = await MemoryPackSerializer.DeserializeAsync<EnumArray<string, SeasonType>>(stream).ConfigureAwait(false);

                Assert.IsNotNull(target);
                Assert.IsNull(target[SeasonType.Autumn]);
                Assert.AreEqual(target[SeasonType.Summer], source[SeasonType.Summer]);
            }
        }

        [TestMethod]
        public async Task EnumArray2DSerializationRoundTripTest()
        {
            EnumArray2D<string, SeasonType, WeatherType> source = new EnumArray2D<string, SeasonType, WeatherType>();
            source[SeasonType.Summer, WeatherType.Snow] = "Almost Impossible";
            source[SeasonType.Summer, WeatherType.Clear] = "Preferred";

            using (MemoryStream stream = new MemoryStream())
            {
                await MemoryPackSerializer.SerializeAsync(stream, source).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);

                stream.Position = 0;
                EnumArray2D<string, SeasonType, WeatherType> target = await MemoryPackSerializer.DeserializeAsync<EnumArray2D<string, SeasonType, WeatherType>>(stream).ConfigureAwait(false);

                Assert.IsNotNull(target);
                Assert.IsNull(target[SeasonType.Winter, WeatherType.Clear]);
                Assert.AreEqual(target[SeasonType.Summer, WeatherType.Clear], source[SeasonType.Summer, WeatherType.Clear]);
            }
        }
    }
}
