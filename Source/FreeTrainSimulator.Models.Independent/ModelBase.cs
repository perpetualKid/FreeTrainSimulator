using System.IO;
using System.Threading;
using System.Threading.Tasks;

using MemoryPack;

namespace FreeTrainSimulator.Models.Independent
{
    public abstract class ModelBase
    {
        private const string SaveStateExtension = ".save";

        public static async ValueTask<T> FromFile<T>(string fileName, CancellationToken cancellationToken) where T : ModelBase
        {
            if (File.Exists(fileName))
            {
                using (FileStream saveFile = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    return await MemoryPackSerializer.DeserializeAsync<T>(saveFile, null, cancellationToken).ConfigureAwait(false);
                }
            }
            return null;
        }

        public static async ValueTask ToFile<T>(string fileName, T model, CancellationToken cancellationToken) where T : ModelBase
        {
            fileName = fileName + SaveStateExtension;
            using (FileStream saveFile = new FileStream(fileName, FileMode.Create, FileAccess.Write))
            {
                await MemoryPackSerializer.SerializeAsync(saveFile, model, null, cancellationToken).ConfigureAwait(false);
                await saveFile.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public string Hash { get; init; }
    }
}
