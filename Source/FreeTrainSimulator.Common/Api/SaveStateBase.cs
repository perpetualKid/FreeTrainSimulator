using System.IO;
using System.Threading;
using System.Threading.Tasks;

using MemoryPack;

namespace FreeTrainSimulator.Common.Api
{
    // Base class for save states
    // All derived classed will be serialized using MemoryPack
    // New properties must be added last, backwards compatibility is assumed as long property order is kept
    public abstract class SaveStateBase
    {
        public static async ValueTask<T> FromFile<T>(string fileName, CancellationToken cancellationToken) where T : SaveStateBase
        {
            using (FileStream saveFile = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                return await MemoryPackSerializer.DeserializeAsync<T>(saveFile, null, cancellationToken).ConfigureAwait(false);
            }
        }

        public static async ValueTask ToFile<T>(string fileName, T saveState, CancellationToken cancellationToken) where T : SaveStateBase
        {
            using (FileStream saveFile = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                await MemoryPackSerializer.SerializeAsync(saveFile, saveState, null, cancellationToken).ConfigureAwait(false);
                await saveFile.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
