using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Independent.Content;
using FreeTrainSimulator.Models.Loader.Shim;

namespace FreeTrainSimulator.Models.Loader.Handler
{
    internal sealed class TestActivityModelHandler : ContentHandlerBase<TestActivityModel, ActivityModelCore>
    {
        public static async ValueTask<FrozenSet<TestActivityModel>> GetTestActivities(ProfileModel profileModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profileModel, nameof(profileModel));

            return FrozenSet<TestActivityModel>.Empty;
        }
    }
}
