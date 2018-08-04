// COPYRIGHT 2011, 2012, 2013 by the Open Rails project.
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

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GNU.Gettext;
using MSTS;
using Orts.Formats.Msts;

namespace ORTS.Menu
{
    public class Route
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Path { get; private set; }

        static GettextResourceManager catalog = new GettextResourceManager("ORTS.Menu");

        internal Route(string path)
        {
            if (Directory.Exists(path))
            {
				string trkFilePath = MSTSPath.GetTRKFileName(path);
                try
                {
					var trkFile = new RouteFile(trkFilePath);
                    Name = trkFile.Tr_RouteFile.Name.Trim();
                    Description = trkFile.Tr_RouteFile.Description.Trim();
                }
                catch
                {
                    Name = "<" + catalog.GetString("load error:") + " " + System.IO.Path.GetFileName(path) + ">";
                }
                if (string.IsNullOrEmpty(Name))
                    Name = "<" + catalog.GetString("unnamed:") + " " + System.IO.Path.GetFileNameWithoutExtension(path) + ">";
                if (string.IsNullOrEmpty(Description))
                    Description = null;
            }
            else
            {
                Name = "<" + catalog.GetString("missing:") + " " + System.IO.Path.GetFileName(path) + ">";
            }
            Path = path;
        }

        public override string ToString()
        {
            return Name;
        }

        public static Task<List<Route>> GetRoutes(Folder folder, CancellationToken token)
        {
            TaskCompletionSource<List<Route>> tcs = new TaskCompletionSource<List<Route>>();

            List<Route> routes = new List<Route>();
            string directory = System.IO.Path.Combine(folder.Path, "ROUTES");
            if (Directory.Exists(directory))
            {
                Parallel.ForEach(Directory.GetDirectories(directory), (routeDirectory, state) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        tcs.SetCanceled();
                        state.Stop();
                    }
                    try
                    {
                        Route route = new Route(routeDirectory);
                        lock (routes)
                        {
                            routes.Add(route);
                        }
                    }
                    catch { }
                });
            }
            tcs.TrySetResult(routes);
            return tcs.Task;
        }
    }
}
