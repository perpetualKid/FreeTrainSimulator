﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Simplified
{
    /// <summary>
    /// Representation of the metadata of a path, where the path is coded in a .pat file. So not the full .pat file,
    /// but just basic information to be used in menus etc.
    /// </summary>
    public class Path : ContentBase
    {

        // MSTS ships with 7 unfinished paths, which cannot be used as they reference tracks that do not exist.
        // MSTS checks for "broken path" before running the simulator and doesn't offer them in the list.
        // ORTS checks for "broken path" when the simulator runs and does offer them in the list.
        // The first activity in Marias Pass is "Explore Longhale" which leads to a "Broken Path" message.
        // The message then confuses users new to ORTS who have just installed it along with MSTS,
        // see https://bugs.launchpad.net/or/+bug/1345172 and https://bugs.launchpad.net/or/+bug/128547
        private static readonly string[] brokenPaths = {
            @"ROUTES\USA1\PATHS\aftstrm(traffic03).pat",
            @"ROUTES\USA1\PATHS\aftstrmtraffic01.pat",
            @"ROUTES\USA1\PATHS\aiphwne2.pat",
            @"ROUTES\USA1\PATHS\aiwnphex.pat",
            @"ROUTES\USA1\PATHS\blizzard(traffic).pat",
            @"ROUTES\USA2\PATHS\longhale.pat",
            @"ROUTES\USA2\PATHS\long-haul west (blizzard).pat",
        };

        /// <summary>Name of the path</summary>
        public string Name { get; private set; }
        /// <summary>Start location of the path</summary>
        public string Start { get; private set; }
        /// <summary>Destination location of the path</summary>
        public string End { get; private set; }
        /// <summary>Full filename of the underlying .pat file</summary>
        public string FilePath { get; private set; }
        /// <summary>Is the path a player path or not</summary>
        public bool PlayerPath { get; private set; }

        /// <summary>
        /// Constructor. This will try to have the requested .pat file parsed for its metadata
        /// </summary>
        /// <param name="filePath">The full name of the .pat file</param>
        internal Path(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    PathFile patFile = new PathFile(filePath);
                    PlayerPath = patFile.PlayerPath;
                    Name = patFile.Name;
                    Start = patFile.Start;
                    End = patFile.End;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Name = $"<{catalog.GetString("load error:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>";
                }
                if (string.IsNullOrEmpty(Name))
                    Name = $"<{catalog.GetString("unnamed:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>";
                if (string.IsNullOrEmpty(Start))
                    Start = $"<{catalog.GetString("unnamed:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>";
                if (string.IsNullOrEmpty(End))
                    End = $"<{catalog.GetString("unnamed:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>";
            }
            else
            {
                Name = Start = End = $"<{catalog.GetString("missing:")} {System.IO.Path.GetFileNameWithoutExtension(filePath)}>";
            }
            FilePath = filePath;
        }

        /// <summary>
        /// A path will be identified by its destination
        /// </summary>
        public override string ToString()
        {
            return End;
        }

        /// <summary>
        /// Return a list of paths that belong to the given route.
        /// </summary>
        /// <param name="routeFolder">The Route for which the paths need to be found</param>
        /// <param name="includeNonPlayerPaths">Selects whether non-player paths are included or not</param>
        public static async Task<IEnumerable<Path>> GetPaths(Orts.Formats.Msts.FolderStructure.ContentFolder.RouteFolder routeFolder, bool includeNonPlayerPaths, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(routeFolder);

            using (SemaphoreSlim addItem = new SemaphoreSlim(1))
            {
                List<Path> result = new List<Path>();
                string pathsDirectory = routeFolder.PathsFolder;

                if (Directory.Exists(pathsDirectory))
                {
                    TransformBlock<string, Path> inputBlock = new TransformBlock<string, Path>
                        (pathFile =>
                        {
                            return new Path(pathFile);
                        },
                        new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token });


                    ActionBlock<Path> actionBlock = new ActionBlock<Path>
                        (async path =>
                        {
                            try
                            {
                                await addItem.WaitAsync(token).ConfigureAwait(false);
                                result.Add(path);
                            }
                            finally
                            {
                                addItem.Release();
                            }
                        });

                    inputBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

                    foreach (string pathFile in Directory.EnumerateFiles(pathsDirectory, "*.pat").
                        Where(f => !brokenPaths.Any(brokenPath => f.EndsWith(brokenPath, StringComparison.OrdinalIgnoreCase))))
                        await inputBlock.SendAsync(pathFile, token).ConfigureAwait(false);

                    inputBlock.Complete();
                    await actionBlock.Completion.ConfigureAwait(false);
                }
                return result.Where(p => (p != null && p.PlayerPath) || includeNonPlayerPaths);
            }
        }

        /// <summary>
        /// Return a list of paths that belong to the given route.
        /// </summary>
        /// <param name="route">The Route for which the paths need to be found</param>
        /// <param name="includeNonPlayerPaths">Selects whether non-player paths are included or not</param>
        public static async Task<IEnumerable<Path>> GetPaths(string pathsDirectory, bool includeNonPlayerPaths, CancellationToken token)
        {
            ArgumentException.ThrowIfNullOrEmpty(pathsDirectory);

            using (SemaphoreSlim addItem = new SemaphoreSlim(1))
            {
                List<Path> result = new List<Path>();

                if (Directory.Exists(pathsDirectory))
                {
                    TransformBlock<string, Path> inputBlock = new TransformBlock<string, Path>
                        (pathFile =>
                        {
                            return new Path(pathFile);
                        },
                        new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token });


                    ActionBlock<Path> actionBlock = new ActionBlock<Path>
                        (async path =>
                        {
                            try
                            {
                                await addItem.WaitAsync(token).ConfigureAwait(false);
                                result.Add(path);
                            }
                            finally
                            {
                                addItem.Release();
                            }
                        });

                    inputBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

                    foreach (string pathFile in Directory.EnumerateFiles(pathsDirectory, "*.pat").
                        Where(f => !brokenPaths.Any(brokenPath => f.EndsWith(brokenPath, StringComparison.OrdinalIgnoreCase))))
                        await inputBlock.SendAsync(pathFile, token).ConfigureAwait(false);

                    inputBlock.Complete();
                    await actionBlock.Completion.ConfigureAwait(false);
                }
                return result.Where(p => (p != null && p.PlayerPath) || includeNonPlayerPaths);
            }
        }

        /// <summary>
        /// Get a path from a certain route with given name.
        /// </summary>
        /// <param name="route">The Route for which the paths need to be found</param>
        /// <param name="name">The (file) name of the path, without directory, any extension allowed</param>
        public static Path GetPath(Orts.Formats.Msts.FolderStructure.ContentFolder.RouteFolder route, string name)
        {
            ArgumentNullException.ThrowIfNull(route);

            string file = route.PathFile(name);
            return new Path(file);
        }

        /// <summary>
        /// Additional information strings about the metadata
        /// </summary>
        /// <returns>array of strings with the user-readable information</returns>
        public string ToInfo()
        {
            return string.Join("\n", catalog.GetString("Start at: {0}", Start), catalog.GetString("Heading to: {0}", End));
        }
    }
}