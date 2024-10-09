using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;

using MemoryPack;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal abstract class ContentHandlerBase<TActual, TBase> where TBase : ModelBase<TBase> where TActual : TBase
    {
        public const string SaveStateExtension = FileNameExtensions.SaveFile;

        private protected static readonly string fileExtension = ModelFileResolver<TBase>.FileExtension;

        private protected static readonly ConcurrentDictionary<string, Lazy<Task<TActual>>> taskLazyCache = new ConcurrentDictionary<string, Lazy<Task<TActual>>>(StringComparer.OrdinalIgnoreCase);
        private protected static readonly ConcurrentDictionary<string, Task<FrozenSet<TActual>>> taskSetCache = new ConcurrentDictionary<string, Task<FrozenSet<TActual>>>(StringComparer.OrdinalIgnoreCase);

        public static async Task<TActual> FromFile<TContainer>(string name, TContainer parent, CancellationToken cancellationToken, bool resolveName = true) where TContainer : ModelBase<TContainer>
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

        public static async Task<TActual> ToFile(TActual model, CancellationToken cancellationToken)
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
            return model;
        }

        public static async Task Create<TContainer>(TActual model, TContainer parent, CancellationToken cancellationToken) where TContainer : ModelBase<TContainer>
        { 
            await Create(model, parent, true, false, cancellationToken).ConfigureAwait(false);
        }

        public static async Task Create<TContainer>(TActual model, TContainer parent, bool saveModel, bool createDirectory, CancellationToken cancellationToken) where TContainer : ModelBase<TContainer>
        {
            ArgumentNullException.ThrowIfNull(model, nameof(model));

            model.Initialize(ModelFileResolver<TBase>.FilePath(model, parent), parent);

            if (saveModel)
                model = await ToFile(model, cancellationToken).ConfigureAwait(false);

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

        protected static Task<TActual> GetCachedTask(ConcurrentDictionary<string, Task<TActual>> cache, string key, Func<Task<TActual>> taskCreator)
        {
            if (!cache.TryGetValue(key, out Task<TActual> cachedTask))
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
        protected static async Task<TBase> Cast(Task<TActual> t) => await t;
    }
}
