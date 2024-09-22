using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class PathModelHandler : ContentHandlerBase<PathModel, PathModelCore>
    {
        // MSTS ships with 7 unfinished paths, which cannot be used as they reference tracks that do not exist.
        // MSTS checks for "broken path" before running the simulator and doesn't offer them in the list.
        // ORTS checks for "broken path" when the simulator runs and does offer them in the list.
        // The first activity in Marias Pass is "Explore Longhale" which leads to a "Broken Path" message.
        // The message then confuses users new to ORTS who have just installed it along with MSTS,
        // see https://bugs.launchpad.net/or/+bug/1345172 and https://bugs.launchpad.net/or/+bug/128547
        //private static readonly string[] brokenPaths = {
        //    @"ROUTES\USA1\PATHS\aftstrm(traffic03).pat",
        //    @"ROUTES\USA1\PATHS\aftstrmtraffic01.pat",
        //    @"ROUTES\USA1\PATHS\aiphwne2.pat",
        //    @"ROUTES\USA1\PATHS\aiwnphex.pat",
        //    @"ROUTES\USA1\PATHS\blizzard(traffic).pat",
        //    @"ROUTES\USA2\PATHS\longhale.pat",
        //    @"ROUTES\USA2\PATHS\long-haul west (blizzard).pat",
        //};

        public static async ValueTask<PathModel> Get(string name, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            return await FromFile(name, routeModel, cancellationToken).ConfigureAwait(false);
        }

        public static async ValueTask<PathModel> Convert(string filePath, RouteModelCore routeModel, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath, nameof(filePath));
            ArgumentNullException.ThrowIfNull(routeModel, nameof(routeModel));

            if (File.Exists(filePath))
            {
                PathFile patFile = new PathFile(filePath);

                PathModel pathModel = new PathModel()
                {
                    Name = string.IsNullOrEmpty(patFile.Name) ? $"unnamed (@ {Path.GetFileNameWithoutExtension(filePath)})" : patFile.Name,
                    Id = patFile.PathID,
                    PlayerPath = patFile.PlayerPath,
                    Start = string.IsNullOrEmpty(patFile.Start) ? $"unnamed (@ {Path.GetFileNameWithoutExtension(filePath)})" : patFile.Start,
                    End = string.IsNullOrEmpty(patFile.End) ? $"unnamed (@ {Path.GetFileNameWithoutExtension(filePath)})" : patFile.End,
                    Tag = Path.GetFileNameWithoutExtension(filePath),
                };
                await Create(pathModel, routeModel, cancellationToken).ConfigureAwait(false);
                return pathModel;
            }
            return null;
        }
    }
}
