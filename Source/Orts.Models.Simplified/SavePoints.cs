using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Orts.Common;

namespace Orts.Models.Simplified
{
    public class SavePoint: ContentBase
    {
        public string Name { get; }
        public string File { get; }
        public string PathName { get; }
        public string RouteName { get; }
        public TimeSpan GameTime { get; }
        public DateTime RealTime { get; }
        public string CurrentTile { get; }
        public string Distance { get; }
        public bool? Valid { get; } // 3 possibilities: invalid, unknown validity, valid
        public string VersionOrBuild { get; }
        public bool IsMultiplayer { get; }
        public bool DebriefEvaluation { get; } //Debrief Eval

        public static async Task<IEnumerable<SavePoint>> GetSavePoints(string directory, string prefix, 
            string routeName, int failedRestoreVersion, string warnings, bool multiPlayer, IEnumerable<Route> mainRoutes, CancellationToken token)
        {
            using (SemaphoreSlim addItem = new SemaphoreSlim(1))
            {
                List<SavePoint> result = new List<SavePoint>();

                TransformBlock<string, SavePoint> inputBlock = new TransformBlock<string, SavePoint>
                    (savePointFile =>
                    {
                        // SavePacks are all in the same folder and activities may have the same name 
                        // (e.g. Short Passenger Run shrtpass.act) but belong to a different route,
                        // so pick only the activities for the current route.
                        SavePoint savePoint = new SavePoint(savePointFile, failedRestoreVersion);
                        if (string.IsNullOrEmpty(routeName) || savePoint.RouteName == routeName)
                        {
                            if (!savePoint.IsMultiplayer ^ multiPlayer)
                                return savePoint;
                        }
                        // In case you receive a SavePack where the activity is recognised but the route has been renamed.
                        // Checks the route is not in your list of routes.
                        // If so, add it with a warning.
                        else if (!mainRoutes.Any(el => el.Name == savePoint.RouteName))
                        {
                            if (!savePoint.IsMultiplayer ^ multiPlayer)
                                return savePoint;
                            // SavePoint a warning to show later.
                            warnings += catalog.GetString("Warning: Save {0} found from a route with an unexpected name:\n{1}.\n\n", savePoint.RealTime, savePoint.RouteName);
                        }
                        return null;
                    },
                    new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token });

                ActionBlock<SavePoint> actionBlock = new ActionBlock<SavePoint>
                        (async savePoint =>
                        {
                            if (null == savePoint)
                                return;
                            try
                            {
                                await addItem.WaitAsync(token).ConfigureAwait(false);
                                result.Add(savePoint);
                            }
                            finally
                            {
                                addItem.Release();
                            }
                        });

                inputBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

                foreach (string saveFile in Directory.EnumerateFiles(directory, $"{prefix}*.save"))
                    await inputBlock.SendAsync(saveFile).ConfigureAwait(false);

                inputBlock.Complete();
                await actionBlock.Completion.ConfigureAwait(false);
                return result;
            }
        }

        private SavePoint(string fileName, int failedRestoreVersion)
        {
            File = fileName;
            Name = System.IO.Path.GetFileNameWithoutExtension(fileName);
            using (BinaryReader inf = new BinaryReader(new FileStream(File, FileMode.Open, FileAccess.Read)))
            {
                try
                {
                    string version = inf.ReadString().Replace("\0", ""); // e.g. "0.9.0.1648" or "X1321" or "" (if compiled locally)
                    string build = inf.ReadString().Replace("\0", ""); // e.g. 0.0.5223.24629 (2014-04-20 13:40:58Z)
                    string versionOrBuild = version.Length > 0 ? version : build;
                    bool? valid = VersionInfo.GetValidity(version, build, failedRestoreVersion);
                    // Read in multiplayer flag/ route/activity/path/player data.
                    // Done so even if not elegant to be compatible with existing save files
                    string routeNameOrMultipl = inf.ReadString();
                    string routeName = "";
                    bool isMultiplayer = false;
                    if (routeNameOrMultipl == "$Multipl$")
                    {
                        isMultiplayer = true;
                        routeName = inf.ReadString(); // Route name
                    }
                    else
                    {
                        routeName = routeNameOrMultipl; // Route name 
                    }

                    string pathName = inf.ReadString(); // Path name
                    TimeSpan gameTime = new DateTime().AddSeconds(inf.ReadInt32()).TimeOfDay; // Game time
                    DateTime realTime = DateTime.FromBinary(inf.ReadInt64()); // Real time
                    float currentTileX = inf.ReadSingle(); // Player TileX
                    float currentTileZ = inf.ReadSingle(); // Player TileZ
                    string currentTile = String.Format("{0:F1}, {1:F1}", currentTileX, currentTileZ);
                    float initialTileX = inf.ReadSingle(); // Initial TileX
                    float initialTileZ = inf.ReadSingle(); // Initial TileZ
                    if (currentTileX < short.MinValue || currentTileX > short.MaxValue || currentTileZ < short.MinValue || currentTileZ > short.MaxValue)
                        throw new InvalidDataException();
                    if (initialTileX < short.MinValue || initialTileX > short.MaxValue || initialTileZ < short.MinValue || initialTileZ > short.MaxValue)
                        throw new InvalidDataException();

                    // DistanceFromInitial using Pythagoras theorem.
                    string distance = String.Format("{0:F1}", Math.Sqrt(Math.Pow(currentTileX - initialTileX, 2) + Math.Pow(currentTileZ - initialTileZ, 2)) * 2048);

                    PathName = pathName;
                    RouteName = routeName.Trim();
                    IsMultiplayer = isMultiplayer;
                    GameTime = gameTime;
                    RealTime = realTime;
                    CurrentTile = currentTile;
                    Distance = distance;
                    Valid = valid;
                    VersionOrBuild = versionOrBuild;

                    //Debrief Eval
                    DebriefEvaluation = System.IO.File.Exists(fileName.Substring(0, fileName.Length - 5) + ".dbfeval");
                }
                catch { }
            }
        }
    }
}
