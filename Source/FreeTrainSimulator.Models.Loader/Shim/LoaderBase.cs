using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent;

using MemoryPack;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public abstract class LoaderBase<T> where T : ModelBase<T>
    {
        public const string SaveStateExtension = ".save";

        public static async ValueTask<T> FromFile(string file, CancellationToken cancellationToken)
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

        public static async ValueTask<T> FromFile<Parent>(string file, Parent parent, CancellationToken cancellationToken) where Parent: ModelBase<Parent>
        {
            string targetFileName = ModelFileResolver<T>.FilePath(file, parent) + SaveStateExtension;
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

        public static async ValueTask ToFile(string file, T model, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            string targetFileName = ModelFileResolver<T>.FilePath(model) + SaveStateExtension;

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
