using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class ContentPathHandler : ContentHandlerBase<PathModel, PathModelCore>
    {
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
                    Name = patFile.Name,
                    PathId = patFile.PathID,
                    PlayerPath = patFile.PlayerPath,
                    Start = patFile.Start,
                    End = patFile.End,
                };
                await Create(pathModel, routeModel, true, false, cancellationToken).ConfigureAwait(false);
                return pathModel;
            }
            return null;
        }
    }
}
