using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent;
using FreeTrainSimulator.Models.Independent.Content;

using MemoryPack;

using Orts.Formats.Msts.Files;

using Orts.Formats.Msts;
using static Orts.Formats.Msts.FolderStructure;

namespace FreeTrainSimulator.Models.Loader.Shim
{
    public abstract class ContentHandlerBase<T> where T : ModelBase<T>
    {
        public const string SaveStateExtension = ".save";

#pragma warning disable CA1000 // Do not declare static members on generic types
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
                if (model?.Initialize(file, null) ?? false)
                {
                    await ToFile(file, model, cancellationToken).ConfigureAwait(false);
                }
            }
            return model;
        }

        public static async ValueTask<T> FromFile<TParent>(string name, TParent parent, CancellationToken cancellationToken, bool resolveName = true) where TParent : ModelBase<TParent>
        {
            string targetFileName = name;
            if (resolveName)
                targetFileName = ModelFileResolver<T>.FilePath(name, parent) + SaveStateExtension;

            T model = null;
            if (File.Exists(targetFileName))
            {
                using (FileStream saveFile = new FileStream(targetFileName, FileMode.Open, FileAccess.Read))
                {
                    model = await MemoryPackSerializer.DeserializeAsync<T>(saveFile, null, cancellationToken).ConfigureAwait(false);
                }
                if (model?.Initialize(targetFileName, parent) ?? false)
                {
                    await ToFile(model, cancellationToken).ConfigureAwait(false);
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

        public static async ValueTask ToFile(T model, CancellationToken cancellationToken)
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

        public static async ValueTask Create<TParent>(T model, TParent parent, bool saveModel, bool createDirectory, CancellationToken cancellationToken) where TParent : ModelBase<TParent>
        {
            model.Initialize(ModelFileResolver<T>.FilePath(model, parent), parent);

            if (saveModel)
                await ToFile(model, cancellationToken).ConfigureAwait(false);

            if (createDirectory)
            {
                string directory = ModelFileResolver<ContentFolderModel>.FolderPath(model);
                if (!Directory.Exists(directory))
                {
                    try
                    {
                        Directory.CreateDirectory(directory);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.Message);
                        throw;
                    }
                }
            }
        }
#pragma warning restore CA1000 // Do not declare static members on generic types
    }
}
