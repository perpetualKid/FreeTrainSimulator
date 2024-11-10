using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal abstract class ContentHandlerBase<TModel> where TModel : ModelBase<TModel>
    {
        public const string SaveStateExtension = FileNameExtensions.SaveFile;

        private protected static readonly string fileExtension = ModelFileResolver<TModel>.FileExtension;
        private protected static readonly ConcurrentDictionary<string, bool> collectionUpdateRequired = new ConcurrentDictionary<string, bool>();

        private protected static readonly ConcurrentDictionary<string, Lazy<Task<TModel>>> taskLazyCache = new ConcurrentDictionary<string, Lazy<Task<TModel>>>(StringComparer.OrdinalIgnoreCase);
        private protected static readonly ConcurrentDictionary<string, Lazy<Task<FrozenSet<TModel>>>> taskLazyCollectionCache = new ConcurrentDictionary<string, Lazy<Task<FrozenSet<TModel>>>>(StringComparer.OrdinalIgnoreCase);

        public static async Task<TModel> FromFile<TContainer>(string name, TContainer parent, CancellationToken cancellationToken, bool resolveName = true) where TContainer : ModelBase<TContainer>
        {
            string targetFileName = name;
            if (resolveName)
                targetFileName = ModelFileResolver<TModel>.FilePath(name, parent) + SaveStateExtension;

            TModel model = null;
            if (File.Exists(targetFileName))
            {
                using (FileStream saveFile = new FileStream(targetFileName, FileMode.Open, FileAccess.Read))
                {
                    model = await MemoryPackSerializer.DeserializeAsync<TModel>(saveFile, null, cancellationToken).ConfigureAwait(false);
                }
                model.Initialize(targetFileName, parent);
            }            
            return model;
        }

        public static async Task<TExtendedModel> FromFile<TExtendedModel, TContainer>(string name, TContainer parent, CancellationToken cancellationToken, bool resolveName = true) where TExtendedModel : TModel where TContainer : ModelBase<TContainer>
        {
            string targetFileName = name;
            if (resolveName)
                targetFileName = ModelFileResolver<TModel>.FilePath(name, parent) + SaveStateExtension;

            TExtendedModel model = null;
            if (File.Exists(targetFileName))
            {
                using (FileStream saveFile = new FileStream(targetFileName, FileMode.Open, FileAccess.Read))
                {
                    model = await MemoryPackSerializer.DeserializeAsync<TExtendedModel>(saveFile, null, cancellationToken).ConfigureAwait(false);
                }
                model.Initialize(targetFileName, parent);
            }
            return model;
        }

        public static async Task<TModel> ToFile(TModel model, CancellationToken cancellationToken)
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

        public static async Task Create<TContainer>(TModel model, TContainer parent, CancellationToken cancellationToken) where TContainer : ModelBase<TContainer>
        { 
            await Create(model, parent, true, false, cancellationToken).ConfigureAwait(false);
        }

        public static async Task Create<TContainer>(TModel model, TContainer parent, bool saveModel, bool createDirectory, CancellationToken cancellationToken) where TContainer : ModelBase<TContainer>
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            model.Initialize(ModelFileResolver<TModel>.FilePath(model, parent), parent);

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

        protected static Task<TModel> GetCachedTask(ConcurrentDictionary<string, Task<TModel>> cache, string key, Func<Task<TModel>> taskCreator)
        {
            if (!cache.TryGetValue(key, out Task<TModel> cachedTask))
            {
                _ = cache.TryAdd(key, cachedTask = taskCreator.Invoke());
            }
            if (cachedTask.IsFaulted)
            {
                Trace.TraceError(cachedTask.Exception?.ToString());
                cache[key] = cachedTask = taskCreator.Invoke();
            }
            return cachedTask;
        }

        /// <summary>
        /// Cast a Full Model task to Base Model task to mimic task covariance
        /// </summary>
        protected static async Task<TModel> Cast<TExtendedModel>(Task<TExtendedModel> t) where TExtendedModel : TModel => await t.ConfigureAwait(false);
    }
}
