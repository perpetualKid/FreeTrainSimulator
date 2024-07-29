using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent;

using MemoryPack;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public abstract class LoaderBase
    {
        private const string SaveStateExtension = ".save";

        public static async ValueTask<T> FromFile<T>(string fileName, CancellationToken cancellationToken) where T : ModelBase<T>
        {
            fileName += SaveStateExtension;
            if (File.Exists(fileName))
            {
                using (FileStream saveFile = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                {
                    return await MemoryPackSerializer.DeserializeAsync<T>(saveFile, null, cancellationToken).ConfigureAwait(false);
                }
            }
            return null;
        }

        public static async ValueTask ToFile<T>(string fileName, T model, CancellationToken cancellationToken) where T : ModelBase<T>
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            fileName += SaveStateExtension;

            try
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(fileName));

                using (FileStream saveFile = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    await MemoryPackSerializer.SerializeAsync(saveFile, model, null, cancellationToken).ConfigureAwait(false);
                    await saveFile.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
                throw;
            }
        }
    }
}
