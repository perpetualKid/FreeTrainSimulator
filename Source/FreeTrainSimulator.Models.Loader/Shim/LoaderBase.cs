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

        public static async ValueTask<T> FromFile<T>(string file, CancellationToken cancellationToken) where T : ModelBase<T>
        {
            string targetFileName = file + SaveStateExtension;
            T model = null;
            if (File.Exists(targetFileName))
            {
                using (FileStream saveFile = new FileStream(targetFileName, FileMode.Open, FileAccess.Read))
                {
                    model = await MemoryPackSerializer.DeserializeAsync<T>(saveFile, null, cancellationToken).ConfigureAwait(false);
                }
                if (model?.Initialize(file) ?? false)
                {
                    await ToFile(file, model, cancellationToken).ConfigureAwait(false);
                }
            }
            return model;
        }

        public static async ValueTask ToFile<T>(string file, T model, CancellationToken cancellationToken) where T : ModelBase<T>
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            string targetFileName = file + SaveStateExtension;
            await model.RefreshModel().ConfigureAwait(false);

            try
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(targetFileName));

                using (FileStream saveFile = new FileStream(targetFileName, FileMode.Create, FileAccess.Write))
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
