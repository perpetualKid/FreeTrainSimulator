using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Settings;

namespace FreeTrainSimulator.Models.Handler
{
    /// <summary>
    /// Handler for <see cref="SavePointModel"/> instances. Save-point models are transient
    /// metadata descriptors (not persisted through the content pipeline) that are loaded
    /// on demand when the user browses saved games.
    /// </summary>
    internal class SavePointModelHandler : ContentHandlerBase<SavePointModel>
    {
        public static Task<SavePointModel> GetCore(SavePointModel savePointModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(savePointModel, nameof(savePointModel));
            return GetCore(savePointModel.Id, savePointModel.Parent, cancellationToken);
        }

        public static Task<SavePointModel> GetCore(string savepointId, RouteModelHeader routeModel, CancellationToken cancellationToken)
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
    }
}
