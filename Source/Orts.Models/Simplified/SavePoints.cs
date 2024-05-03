using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;

using MemoryPack;

using Orts.Models.State;

namespace Orts.Models.Simplified
{
    public class SavePoint : ContentBase
    {
        public string Name => System.IO.Path.GetFileNameWithoutExtension(File);
        public string File { get; private set; }
        public string PathName { get; private set; }
        public string RouteName { get; private set; }
        public TimeSpan GameTime { get; private set; }
        public DateTime RealTime { get; private set; }
        public string CurrentTile { get; private set; }
        public string Distance { get; private set; }
        public bool? Valid { get; private set; } // 3 possibilities: invalid, unknown validity, valid
        public string ProgramVersion { get; private set; }
        public bool IsMultiplayer { get; private set; }
        public bool DebriefEvaluation { get; private set; } //Debrief Eval

        public GameSaveState SaveState { get; private set; }

        public static async Task<IEnumerable<SavePoint>> GetSavePoints(string directory, string prefix, string routeName,
            StringBuilder warnings, bool multiPlayer, IEnumerable<Route> mainRoutes, CancellationToken token)
        {
            List<SavePoint> result = new List<SavePoint>();
            using (SemaphoreSlim addItem = new SemaphoreSlim(1))
            {
                await Parallel.ForEachAsync(Directory.EnumerateFiles(directory, System.IO.Path.ChangeExtension($"{prefix}*", FileNameExtensions.SaveFile)), token, async (fileName, innerToken) =>
                {
                    SavePoint gameSaveState = await FromGameSaveState(fileName);

                    try
                    {
                        await addItem.WaitAsync(innerToken).ConfigureAwait(false);
                        if (gameSaveState != null)
                        {
                            // SavePacks are all in the same folder and activities may have the same name 
                            // (e.g. Short Passenger Run shrtpass.act) but belong to a different route,
                            // so pick only the activities for the current route.
                            if (string.IsNullOrEmpty(routeName) || gameSaveState.RouteName == routeName)
                            {
                                if (!gameSaveState.IsMultiplayer ^ multiPlayer)
                                {
                                    result.Add(gameSaveState);
                                }
                            }
                            // In case you receive a SavePack where the activity is recognised but the route has been renamed.
                            // Checks the route is not in your list of routes.
                            // If so, add it with a warning.
                            else if (mainRoutes != null && !mainRoutes.Any(route => route.Name == gameSaveState.RouteName))
                            {
                                if (!gameSaveState.IsMultiplayer ^ multiPlayer)
                                {
                                    result.Add(gameSaveState);
                                }
                                // Save a warning to show later.
                                warnings?.Append(catalog.GetString($"Warning: Save {gameSaveState.RealTime} found from a route with an unexpected name:\n{gameSaveState.RouteName}.\n\n"));
                            }
                            else
                            {
                                result.Add(gameSaveState);
                            }
                        }
                        else
                        {
                            // Save a warning to show later.
                            warnings?.Append(catalog.GetString($"Error: File '{System.IO.Path.GetFileName(fileName)}' is invalid or corrupted.\n"));
                            result.Add(new SavePoint()
                            {
                                File = fileName,
                                Distance = double.NaN.ToString(),
                                RouteName = "<Invalid Savepoint>",
                            });
                        }
                    }
                    finally
                    {
                        addItem.Release();
                    }
                });
            }
            return result;
        }

        private static async Task<SavePoint> FromGameSaveState(string fileName)
        {
            try
            {
                GameSaveState saveState = await GameSaveState.FromFile<GameSaveState>(fileName);
                SavePoint result = new SavePoint()
                {
                    File = fileName,
                    ProgramVersion = saveState.GameVersion,
                    Valid = saveState.Valid,
                    RouteName = saveState.RouteName,
                    PathName = saveState.PathName,
                    GameTime = new DateTime().AddSeconds(saveState.GameTime).TimeOfDay,
                    RealTime = saveState.RealSaveTime.ToLocalTime(),
                    CurrentTile = $"{saveState.PlayerPosition.TileX:F0}, {saveState.PlayerPosition.TileZ:F0}",
                    Distance = $"{Math.Sqrt(Math.Pow(saveState.PlayerPosition.TileX - saveState.InitalTile.X, 2) + Math.Pow(saveState.PlayerPosition.TileZ - saveState.InitalTile.Z, 2)) * Common.Position.WorldPosition.TileSize:F1}",
                    IsMultiplayer = saveState.MultiplayerGame,
                    //Debrief Eval
                    DebriefEvaluation = saveState.ActivityEvaluationState != null,
                    SaveState = saveState,
                };
                return result;
            }
            catch (MemoryPackSerializationException)
            {
                //not a valid savepoint
            }
            return null;
        }
    }
}
