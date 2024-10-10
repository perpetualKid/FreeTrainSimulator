using System;
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
        public static async ValueTask<FrozenSet<TestActivityModel>> LoadTestActivities(this ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            return await TestActivityModelHandler.GetTestActivities(profileModel, cancellationToken).ConfigureAwait(false);
        }
    }
}
