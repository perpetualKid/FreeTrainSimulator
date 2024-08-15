using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal abstract class ContentHandlerBase<TActual, TBase> where TBase : ModelBase<TBase> where TActual : TBase
    {
        public const string SaveStateExtension = ".save";

#pragma warning disable CA1000 // Do not declare static members on generic types
        public static async ValueTask<TActual> FromFile<TContainer>(string name, TContainer parent, CancellationToken cancellationToken, bool resolveName = true) where TContainer : ModelBase<TContainer>
        {
            string targetFileName = name;
            if (resolveName)
                targetFileName = ModelFileResolver<TBase>.FilePath(name, parent) + SaveStateExtension;

            TActual model = null;
            if (File.Exists(targetFileName))
            {
                using (FileStream saveFile = new FileStream(targetFileName, FileMode.Open, FileAccess.Read))
                {
                    model = await MemoryPackSerializer.DeserializeAsync<TActual>(saveFile, null, cancellationToken).ConfigureAwait(false);
                }
                model.Initialize(targetFileName, parent);
            }
            return model;
        }

        public static async ValueTask ToFile(TActual model, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            string targetFileName = ModelFileResolver<TBase>.FilePath(model) + SaveStateExtension;

            model.RefreshModel();

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

        public static async ValueTask Create<TContainer>(TActual model, TContainer parent, bool saveModel, bool createDirectory, CancellationToken cancellationToken) where TContainer : ModelBase<TContainer>
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            model.Initialize(ModelFileResolver<TBase>.FilePath(model, parent), parent);

            if (saveModel)
                await ToFile(model, cancellationToken).ConfigureAwait(false);

            if (createDirectory)
            {
                string directory = ModelFileResolver<FolderModel>.FolderPath(model);
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
