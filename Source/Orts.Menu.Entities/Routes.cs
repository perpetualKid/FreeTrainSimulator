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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Orts.Formats.Msts;
using ORTS.Common.Msts;

namespace Orts.Menu.Entities
{
    public class Route: ContentBase
    {
        public string Name { get; private set; }
        public string RouteID { get; private set; }
        public string Description { get; private set; }
        public string Path { get; private set; }

        internal Route(string path)
        {
            if (Directory.Exists(path))
            {
				string trkFilePath = MstsPath.GetTRKFileName(path);
                try
                {
					var trkFile = new RouteFile(trkFilePath);
                    Name = trkFile.Tr_RouteFile.Name.Trim();
                    RouteID = trkFile.Tr_RouteFile.RouteID;
                    Description = trkFile.Tr_RouteFile.Description.Trim();
                }
                catch
                {
                    Name = $"<{catalog.GetString("load error:")} {System.IO.Path.GetFileName(path)}>";
                }
                if (string.IsNullOrEmpty(Name))
                    Name = $"<{catalog.GetString("unnamed:")} {System.IO.Path.GetFileNameWithoutExtension(path)}>";
                if (string.IsNullOrEmpty(Description))
                    Description = null;
            }
            else
            {
                Name = $"<{catalog.GetString("missing:")} {System.IO.Path.GetFileName(path)}>";
            }
            Path = path;
        }

        public override string ToString()
        {
            return Name;
        }

        public static Task<List<Route>> GetRoutes(Folder folder, CancellationToken token)
        {
            SemaphoreSlim addItem = new SemaphoreSlim(1);
            List<Route> routes = new List<Route>();
            string directory = System.IO.Path.Combine(folder.Path, "ROUTES");
            if (Directory.Exists(directory))
            {
                try
                {
                    Parallel.ForEach(Directory.GetDirectories(directory),
                        new ParallelOptions() { CancellationToken = token },
                        (routeDirectory, state) =>
                    {
                        try
                        {
                            Route route = new Route(routeDirectory);
                            addItem.Wait(token);
                            routes.Add(route);
                        }
                        catch { }
                        finally { addItem.Release(); }
                    });
                }
                catch (OperationCanceledException) { }
                if (token.IsCancellationRequested)
                    return Task.FromCanceled<List<Route>>(token);
            }
            return Task.FromResult(routes);
        }
    }
}
