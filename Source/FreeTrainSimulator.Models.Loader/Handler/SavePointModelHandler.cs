using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;
using FreeTrainSimulator.Models.Loader.Shim;
using FreeTrainSimulator.Models.State;

using MemoryPack;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    // Savepoints models are transient, not persisted
    internal class SavePointModelHandler : ContentHandlerBase<SavePointModel>
    {
        internal const string SourceNameKey = "MstsSourceRoute";

        public static ValueTask<SavePointModel> GetCore(SavePointModel savePointModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(savePointModel, nameof(savePointModel));
            return GetCore(savePointModel.Id, savePointModel.Parent, cancellationToken);
        }

        public static async ValueTask<SavePointModel> GetCore(string savepointId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(savepointId);

            if (!taskLazyCache.TryGetValue(key, out Lazy<Task<SavePointModel>> modelTask) || (modelTask.IsValueCreated && modelTask.Value.IsFaulted))
            {
                taskLazyCache[key] = modelTask = new Lazy<Task<SavePointModel>>(FromFile(savepointId, routeModel, cancellationToken));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            SavePointModel savePointModel = await modelTask.Value.ConfigureAwait(false);

            if (savePointModel?.RefreshRequired ?? false)
            {
                taskLazyCache[key] = new Lazy<Task<SavePointModel>>(() => Cast(Convert(savePointModel, cancellationToken)));
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return savePointModel;
        }

        public static async ValueTask<FrozenSet<SavePointModel>> GetSavePoints(RouteModelCore routeModel, string activityPrefix, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(activityPrefix);

            if (collectionUpdateRequired.TryRemove(key, out _) || !taskLazyCollectionCache.TryGetValue(key, out Lazy<Task<FrozenSet<SavePointModel>>> modelSetTask) || (modelSetTask.IsValueCreated && modelSetTask.Value.IsFaulted))
            {
                taskLazyCollectionCache[key] = modelSetTask = new Lazy<Task<FrozenSet<SavePointModel>>>(() => ExpandSavePointModels(routeModel, activityPrefix, cancellationToken));
            }

            return await modelSetTask.Value.ConfigureAwait(false);
        }

        public static async Task<FrozenSet<SavePointModel>> ExpandSavePointModels(RouteModelCore routeModel, string activityPrefix, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            ConcurrentBag<SavePointModel> results = new ConcurrentBag<SavePointModel>();

            string sourceFolder = RuntimeInfo.UserDataFolder;

            if (Directory.Exists(sourceFolder))
            {
                // load existing MSTS files
                ConcurrentBag<string> savepointFiles = new ConcurrentBag<string>(Directory.EnumerateFiles(sourceFolder, Path.ChangeExtension($"{activityPrefix}*", FileNameExtensions.SaveFile)));

                await Parallel.ForEachAsync(savepointFiles, cancellationToken, async (savePoint, token) =>
                {
                    Lazy<Task<SavePointModel>> modelTask = new Lazy<Task<SavePointModel>>(Cast(Convert(savePoint, routeModel, cancellationToken)));

                    SavePointModel savePointModel = await modelTask.Value.ConfigureAwait(false);

                    if (null != savePointModel)
                    {
                        string key = savePointModel.Hierarchy();
                        results.Add(savePointModel);
                        taskLazyCache[key] = modelTask;
                    }
                }).ConfigureAwait(false);
            }

            FrozenSet<SavePointModel> result = results.ToFrozenSet();
            string key = routeModel.Hierarchy();
            taskLazyCollectionCache[key] = new Lazy<Task<FrozenSet<SavePointModel>>>(Task.FromResult(result));
            return result;
        }

        private static Task<SavePointModel> Convert(SavePointModel savePointModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(savePointModel, nameof(savePointModel));

            return Convert(Path.Combine(RuntimeInfo.UserDataFolder, savePointModel.Tags[SourceNameKey]), savePointModel.Parent, cancellationToken);
        }

        private static async Task<SavePointModel> Convert(string filePath, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            if (File.Exists(filePath))
            {
                try
                {
                    GameSaveState saveState = await Common.Api.SaveStateBase.FromFile<GameSaveState>(filePath, cancellationToken).ConfigureAwait(false);
                    SavePointModel result = new SavePointModel()
                    {
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        Version = saveState.GameVersion,
                        ValidState = saveState.Valid,
                        RouteName = saveState.RouteName,
                        PathName = saveState.PathName,
                        GameTime = new DateTime().AddSeconds(saveState.GameTime).TimeOfDay,
                        RealTime = saveState.RealSaveTime.ToLocalTime(),
                        CurrentTile = saveState.PlayerLocation.Tile,
                        DistanceTravelled = Math.Sqrt(WorldLocation.GetDistanceSquared(saveState.PlayerLocation, saveState.InitialLocation)),
                        MultiplayerGame = saveState.MultiplayerGame,
                        //Debrief Eval
                        DebriefEvaluation = saveState.ActivityEvaluationState != null,
                        Tags = new Dictionary<string, string> { { SourceNameKey, filePath } },
                    };
                    return result;
                }
                catch (MemoryPackSerializationException)
                {
                    //not a valid savepoint
                    Trace.TraceWarning($"Savepoint file {filePath} is not a valid save point file.");

                    return new SavePointModel()
                    {
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        Version = "n/a",
                        ValidState = false,
                        RouteName = routeModel.Name,
                        PathName = "<Invalid Savepoint>",
                        GameTime = TimeSpan.MinValue,
                        RealTime = DateTime.MinValue.ToLocalTime(),
                        CurrentTile = Tile.Zero,
                        DistanceTravelled = 0,
                        MultiplayerGame = false,
                        //Debrief Eval
                        DebriefEvaluation = false,
                        Tags = new Dictionary<string, string> { { SourceNameKey, filePath } },
                    };
                }
            }
            else
            {
                Trace.TraceWarning($"Savepoint file {filePath} refers to non-existing file.");
                return null;
            }
        }
    }
}
