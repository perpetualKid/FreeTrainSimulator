﻿using System;
using System.Collections.Frozen;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{

    public static class ModelBaseExtensions
    { 
        public static FrozenSet<T> Replace<T>(this FrozenSet<T> collection, T oldValue, T newValue) where T: ModelBase<T>
        {
            ArgumentNullException.ThrowIfNull(collection, nameof(collection));
            return collection.Where((model) => model != oldValue).Append(newValue).ToFrozenSet(); //Replacing the existing route model in the parent folder, with this new instance
        }
    }

    public static class PathModelExtensions
    {
        public static async ValueTask<PathModel> Convert(this PathModel pathModel, CancellationToken cancellationToken)
        {
            return pathModel != null ? await PathModelHandler.Convert(pathModel.Name, (pathModel as IFileResolve).Container as RouteModel, cancellationToken).ConfigureAwait(false) : pathModel;
        }
    }
}