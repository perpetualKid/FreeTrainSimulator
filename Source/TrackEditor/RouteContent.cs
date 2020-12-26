using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Orts.Models.Simplified;

namespace Orts.TrackEditor
{
    public class RouteContent
    {
        public async Task FindRoutes(Folder routeFolder)
        {
            List<Route> newRoutes = (await Task.Run(() => Route.GetRoutes(routeFolder, System.Threading.CancellationToken.None)).ConfigureAwait(true)).OrderBy(r => r.ToString()).ToList();

        }
    }
}
