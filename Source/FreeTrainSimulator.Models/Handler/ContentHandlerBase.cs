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
    /// <summary>
    /// Abstract base class for content model handlers, providing MemoryPack-based file
    /// serialization/deserialization, task-level caching of individual models and model
    /// collections, and directory management. Concrete handlers derive from this to add
    /// domain-specific loading and query logic.
    /// </summary>
    /// <typeparam name="TModel">The model type managed by this handler.</typeparam>
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
        /// Returns the cached load task for a single model identified by <paramref name="id"/> within
        /// <paramref name="parent"/>, starting and caching a fresh <see cref="FromFile{TContainer}"/> load when none
        /// is cached or the cached load faulted, and marking the parent collection for refresh.
        /// </summary>
        protected static Task<TModel> GetOrAddCore<TContainer>(string id, TContainer parent, CancellationToken cancellationToken) where TContainer : ModelBase
        {
            ArgumentNullException.ThrowIfNull(parent, nameof(parent));
            string key = parent.Hierarchy(id);

            if (!modelTaskCache.TryGetValue(key, out Task<TModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(id, parent, cancellationToken);
                collectionUpdateRequired[parent.Hierarchy()] = true;
            }

            return modelTask;
        }

        /// <summary>
        /// Returns the cached collection task for all models under <paramref name="parent"/>, reloading from disk
        /// when the collection was marked for refresh, is uncached, or the cached load faulted.
        /// </summary>
        protected static Task<ImmutableArray<TModel>> GetOrAddCollection<TContainer>(TContainer parent, CancellationToken cancellationToken) where TContainer : ModelBase
        {
            ArgumentNullException.ThrowIfNull(parent, nameof(parent));
            string key = parent.Hierarchy();

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<TModel>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = LoadFromFolder(parent, cancellationToken);
            }

            return modelSetTask;
        }

        private static async Task<ImmutableArray<TModel>> LoadFromFolder<TContainer>(TContainer parent, CancellationToken cancellationToken) where TContainer : ModelBase
        {
            string folder = ModelFileResolver<TModel>.FolderPath(parent);
            string pattern = ModelFileResolver<TModel>.WildcardSavePattern;

            ConcurrentBag<TModel> results = new ConcurrentBag<TModel>();

            // Load each model file in the parent folder; a missing folder simply yields an empty collection.
            if (Directory.Exists(folder))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(folder, pattern), cancellationToken, async (file, token) =>
                {
                    string id = Path.GetFileNameWithoutExtension(file);

                    if (id.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        id = id[..^fileExtension.Length];

                    TModel model = await GetOrAddCore(id, parent, token).ConfigureAwait(false);
                    if (null != model)
                        results.Add(model);
                }).ConfigureAwait(false);
            }

            return results.ToImmutableArray();
        }

        /// <summary>
        /// Cast a Full Model task to Base Model task to mimic task covariance
        /// </summary>
        protected static async Task<TModel> Cast<TExtendedModel>(Task<TExtendedModel> t) where TExtendedModel : TModel => t != null ? await t.ConfigureAwait(false) : null;
    }
}
