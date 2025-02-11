using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Handler;
using FreeTrainSimulator.Models.Imported.State;
using FreeTrainSimulator.Models.Settings;

using MemoryPack;

namespace FreeTrainSimulator.Models.Imported.ImportHandler
{
    // Savepoints models are transient, not persisted
    internal class SavePointModelHandler : ContentHandlerBase<SavePointModel>
    {
        internal const string SourceNameKey = "MstsSourceRoute";

        public static Task<SavePointModel> GetCore(SavePointModel savePointModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(savePointModel, nameof(savePointModel));
            return GetCore(savePointModel.Id, savePointModel.Parent, cancellationToken);
        }

        public static Task<SavePointModel> GetCore(string savepointId, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(savepointId);

            if (!modelTaskCache.TryGetValue(key, out Task<SavePointModel> modelTask) || modelTask.IsFaulted)
            {
                modelTaskCache[key] = modelTask = FromFile(savepointId, routeModel, cancellationToken);
                collectionUpdateRequired[routeModel.Hierarchy()] = true;
            }

            return modelTask;
        }

        public static Task<ImmutableArray<SavePointModel>> GetSavePoints(RouteModelCore routeModel, string activityPrefix, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));
            string key = routeModel.Hierarchy(activityPrefix);

            if (collectionUpdateRequired.TryRemove(key, out _) || !modelSetTaskCache.TryGetValue(key, out Task<ImmutableArray<SavePointModel>> modelSetTask) || modelSetTask.IsFaulted)
            {
                modelSetTaskCache[key] = modelSetTask = ExpandSavePointModels(routeModel, activityPrefix, cancellationToken);
            }

            return modelSetTask;
        }

        public static async Task<ImmutableArray<SavePointModel>> ExpandSavePointModels(RouteModelCore routeModel, string activityPrefix, CancellationToken cancellationToken)
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
                    Task<SavePointModel> modelTask = Cast(Convert(savePoint, routeModel, cancellationToken));

                    SavePointModel savePointModel = await modelTask.ConfigureAwait(false);

                    if (null != savePointModel)
                    {
                        string key = savePointModel.Hierarchy();
                        results.Add(savePointModel);
                        modelTaskCache[key] = modelTask;
                    }
                }).ConfigureAwait(false);
            }

            ImmutableArray<SavePointModel> result = results.ToImmutableArray();
            string key = routeModel.Hierarchy();
            modelSetTaskCache[key] = Task.FromResult(result);
            return result;
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
                        Route = saveState.Route,
                        Path = saveState.Path,
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

                    DateTime createdTime = DateTime.MinValue;
                    string[] nameParts = Path.GetFileNameWithoutExtension(filePath).Split(' ');
                    if (nameParts.Length > 2 && char.IsAsciiDigit(nameParts[^2][0]) && char.IsAsciiDigit(nameParts[^2][0]))
                    {
                        _ = DateTime.TryParse(string.Concat(nameParts[^2], ' ', nameParts[^1]), new DateTimeFormatInfo()
                        {
                            TimeSeparator = ".",
                            DateSeparator = "-",
                        },
                        out createdTime);
                    }

                    return new SavePointModel()
                    {
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        Version = "n/a",
                        ValidState = false,
                        Route = routeModel.Id,
                        Path = "<Invalid Savepoint>",
                        GameTime = TimeSpan.Zero,
                        RealTime = createdTime,
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
