using System.IO;
using System.Threading.Tasks;

using MemoryPack;

namespace FreeTrainSimulator.Common.Api
{
    // Base class for save states
    // All derived classed will be serialized using MemoryPack
    // New properties must be added last, backwards compatibility is assumed as long property order is kept
    public abstract class SaveStateBase
    {
        public static async Task<T> FromFile<T>(string fileName) where T : SaveStateBase
        {
            using (FileStream saveFile = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                return await MemoryPackSerializer.DeserializeAsync<T>(saveFile).ConfigureAwait(false);
            }
        }

        public static async Task ToFile<T>(string fileName, T saveState) where T : SaveStateBase
        {
            using (FileStream saveFile = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                await MemoryPackSerializer.SerializeAsync(saveFile, saveState).ConfigureAwait(false);
                await saveFile.FlushAsync().ConfigureAwait(false);
            }
        }
    }
}
