﻿using System;
using System.Collections.Frozen;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Independent.Base;
using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Independent.Settings;
using FreeTrainSimulator.Models.Loader.Handler;

namespace FreeTrainSimulator.Models.Loader.Shim
{

    public static class ActivityModelExtensions
    {


        public static async ValueTask<PathModel> Convert(this PathModel pathModel, CancellationToken cancellationToken)
        {
            return pathModel != null ? await PathModelHandler.Convert(pathModel.Name, (pathModel as IFileResolve).Container as RouteModel, cancellationToken).ConfigureAwait(false) : pathModel;
        }
    }
}