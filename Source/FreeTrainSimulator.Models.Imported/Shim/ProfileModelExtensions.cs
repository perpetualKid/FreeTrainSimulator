using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Imported.ImportHandler;
using FreeTrainSimulator.Models.Shim;

namespace FreeTrainSimulator.Models.Imported.Shim
{
    public static class ProfileModelExtensions
    {
        public static Task<ProfileModel> Setup(this ProfileModel profileModel, IProgress<int> progressClient, CancellationToken cancellationToken)
        {
            return ContentModelConverter.SetupContent(profileModel, true, progressClient, cancellationToken);
        }
        public static Task<ProfileModel> GetOrCreate(this FrozenSet<ProfileModel> profiles, string profileName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(profiles, nameof(profiles));
            ProfileModel profileModel = profiles.GetByName(profileName);
            return null != profileModel
                ? Task.FromResult(profileModel)
                : Models.Shim.ProfileModelExtensions.Setup(profileModel, profileName, Enumerable.Empty<(string, string)>(), cancellationToken);
        }

        public static Task<ProfileModel> Convert(this ProfileModel profileModel, bool force, CancellationToken cancellationToken)
        {
            return ContentModelConverter.ConvertContent(profileModel, force, cancellationToken);
        }

    }
}
