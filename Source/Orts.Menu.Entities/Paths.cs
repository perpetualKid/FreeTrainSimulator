// COPYRIGHT 2012, 2013, 2014 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using Orts.Formats.Msts.Files;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Orts.Menu.Entities
{
    /// <summary>
    /// Representation of the metadata of a path, where the path is coded in a .pat file. So not the full .pat file,
    /// but just basic information to be used in menus etc.
    /// </summary>
    public class Path: ContentBase
    {
        /// <summary>Name of the path</summary>
        public string Name { get; private set; }
        /// <summary>Start location of the path</summary>
        public string Start { get; private set; }
        /// <summary>Destination location of the path</summary>
        public string End { get; private set; }
        /// <summary>Full filename of the underlying .pat file</summary>
        public string FilePath { get; private set; }
        /// <summary>Is the path a player path or not</summary>
        public bool IsPlayerPath { get; private set; }

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
                    var patFile = new PathFile(filePath);
                    this.IsPlayerPath = patFile.IsPlayerPath;
                    Name = patFile.Name.Trim();
                    Start = patFile.Start.Trim();
                    End = patFile.End.Trim();
                }
                catch
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
        /// <param name="route">The Route for which the paths need to be found</param>
        /// <param name="includeNonPlayerPaths">Selects whether non-player paths are included or not</param>
        public static Task<List<Path>> GetPaths(Route route, bool includeNonPlayerPaths, CancellationToken token)
        {
            SemaphoreSlim addItem = new SemaphoreSlim(1);
            List<Path> paths = new List<Path>();
            string pathsDirectory = route.RouteFolder.PathsFolder;
            if (Directory.Exists(pathsDirectory))
            {
                try
                {
                    Parallel.ForEach(Directory.GetFiles(pathsDirectory, "*.pat"),
                        new ParallelOptions() { CancellationToken = token },
                        (file, state) =>
                    {
                        try
                        {
                            Path path = new Path(file);

                            if (includeNonPlayerPaths || path.IsPlayerPath)
                            {
                            // Suppress the 7 broken paths shipped with MSTS
                            //
                            // MSTS ships with 7 unfinished paths, which cannot be used as they reference tracks that do not exist.
                            // MSTS checks for "broken path" before running the simulator and doesn't offer them in the list.
                            // ORTS checks for "broken path" when the simulator runs and does offer them in the list.
                            // The first activity in Marias Pass is "Explore Longhale" which leads to a "Broken Path" message.
                            // The message then confuses users new to ORTS who have just installed it along with MSTS,
                            // see https://bugs.launchpad.net/or/+bug/1345172 and https://bugs.launchpad.net/or/+bug/128547
                            if (!file.EndsWith(@"ROUTES\USA1\PATHS\aftstrm(traffic03).pat", StringComparison.OrdinalIgnoreCase)
                                    && !file.EndsWith(@"ROUTES\USA1\PATHS\aftstrmtraffic01.pat", StringComparison.OrdinalIgnoreCase)
                                    && !file.EndsWith(@"ROUTES\USA1\PATHS\aiphwne2.pat", StringComparison.OrdinalIgnoreCase)
                                    && !file.EndsWith(@"ROUTES\USA1\PATHS\aiwnphex.pat", StringComparison.OrdinalIgnoreCase)
                                    && !file.EndsWith(@"ROUTES\USA1\PATHS\blizzard(traffic).pat", StringComparison.OrdinalIgnoreCase)
                                    && !file.EndsWith(@"ROUTES\USA2\PATHS\longhale.pat", StringComparison.OrdinalIgnoreCase)
                                    && !file.EndsWith(@"ROUTES\USA2\PATHS\long-haul west (blizzard).pat", StringComparison.OrdinalIgnoreCase)
                                    )
                                {
                                    addItem.Wait(token);
                                    paths.Add(path);
                                }
                            }
                        }
                        catch { }
                        finally { addItem.Release(); }
                    });
                }
                catch (OperationCanceledException) { }
                if (token.IsCancellationRequested)
                    return Task.FromCanceled<List<Path>>(token);
            }
            return Task.FromResult(paths);
        }

        /// <summary>
        /// Get a path from a certain route with given name.
        /// </summary>
        /// <param name="route">The Route for which the paths need to be found</param>
        /// <param name="name">The (file) name of the path, without directory, any extension allowed</param>
        /// <param name="allowNonPlayerPath">Are non-player paths allowed?</param>
        /// <returns>The path that has been found and is allowed, or null</returns>
        public static Path GetPath(Route route, string name, bool allowNonPlayerPath)
        {
            Path path;
            string file = route.RouteFolder.PathFile(name);
            try
            {
                path = new Path(file);
            }
            catch
            {
                path = null;
            }

            bool pathIsAllowed = allowNonPlayerPath || path.IsPlayerPath;
            if (!pathIsAllowed)
            {
                path = null;
            }

            return path;
        }

        /// <summary>
        /// Additional information strings about the metadata
        /// </summary>
        /// <returns>array of strings with the user-readable information</returns>
        public string[] ToInfo()
        {
            string[] infoString = new string[] {
                catalog.GetStringFmt("Start at: {0}", Start),
                catalog.GetStringFmt("Heading to: {0}", End),
            };

            return (infoString);
        }
    }
}
