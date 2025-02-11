using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Handler
{
    public abstract class ContentHandlerBase<TModel> where TModel : ModelBase
    {
        public const string SaveStateExtension = FileNameExtensions.SaveFile;

        protected static readonly string fileExtension = ModelFileResolver<TModel>.FileExtension;
        protected static readonly ConcurrentDictionary<string, bool> collectionUpdateRequired = new ConcurrentDictionary<string, bool>();

        protected static readonly ConcurrentDictionary<string, Task<TModel>> modelTaskCache = new ConcurrentDictionary<string, Task<TModel>>(StringComparer.OrdinalIgnoreCase);
        protected static readonly ConcurrentDictionary<string, Task<ImmutableArray<TModel>>> modelSetTaskCache = new ConcurrentDictionary<string, Task<ImmutableArray<TModel>>>(StringComparer.OrdinalIgnoreCase);

        internal protected static async Task<TModel> FromFile<TContainer>(string name, TContainer parent, CancellationToken cancellationToken, bool resolveName = true) where TContainer : ModelBase
        {
            string targetFileName = name;
            if (resolveName)
                targetFileName = ModelFileResolver<TModel>.FilePath(name, parent) + SaveStateExtension;

            TModel model = null;
            if (File.Exists(targetFileName))
            {
                try
                {
                    using (FileStream saveFile = new FileStream(targetFileName, FileMode.Open, FileAccess.Read))
                    {
                        model = await MemoryPackSerializer.DeserializeAsync<TModel>(saveFile, null, cancellationToken).ConfigureAwait(false);
                    }
                    model.Initialize(parent);
                }
                catch (MemoryPackSerializationException) { }
            }
            return model;
        }

        internal protected static async Task<TExtendedModel> FromFile<TExtendedModel, TContainer>(string name, TContainer parent, CancellationToken cancellationToken, bool resolveName = true) where TExtendedModel : TModel where TContainer : ModelBase
        {
            string targetFileName = name;
            if (resolveName)
                targetFileName = ModelFileResolver<TModel>.FilePath<TContainer>(name, parent) + SaveStateExtension;

            TExtendedModel model = null;
            if (File.Exists(targetFileName))
            {
                using (FileStream saveFile = new FileStream(targetFileName, FileMode.Open, FileAccess.Read))
                {
                    model = await MemoryPackSerializer.DeserializeAsync<TExtendedModel>(saveFile, null, cancellationToken).ConfigureAwait(false);
                }
                model.Initialize(parent);
            }
            return model;
        }

        internal protected static async Task<TActual> ToFile<TActual>(TActual model, CancellationToken cancellationToken) where TActual : TModel
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            string targetFileName = ModelFileResolver<TModel>.FilePath(model) + SaveStateExtension;

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
            return model;
        }

        internal protected static Task Create<TActual, TContainer>(TActual model, TContainer parent, CancellationToken cancellationToken) where TContainer : ModelBase where TActual : TModel
        {
            return Create(model, parent, true, false, cancellationToken);
        }

        internal protected static async Task Create<TActual, TContainer>(TActual model, TContainer parent, bool saveModel, bool createDirectory, CancellationToken cancellationToken) where TContainer : ModelBase where TActual : TModel
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            model.Initialize(parent);

            if (saveModel)
                model = await ToFile(model, cancellationToken).ConfigureAwait(false);

            if (createDirectory)
            {
                string directory = ModelFileResolver<TModel>.FolderPath(model);
                if (!Directory.Exists(directory))
                {
                    try
                    {
                        _ = Directory.CreateDirectory(directory);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.Message);
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Cast a Full Model task to Base Model task to mimic task covariance
        /// </summary>
        protected static async Task<TModel> Cast<TExtendedModel>(Task<TExtendedModel> t) where TExtendedModel : TModel => t != null ? await t.ConfigureAwait(false) : null;
    }
}
