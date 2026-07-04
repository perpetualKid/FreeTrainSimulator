using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    /// <summary>
    /// Handler for wagon set (consist) models. Loads individual <see cref="WagonSetModel"/>
    /// instances, enumerates all consists in a content folder, and extracts a distinct
    /// locomotive list from the available wagon sets.
    /// </summary>
    internal sealed class WagonSetModelHandler : ContentHandlerBase<WagonSetModel>
    {
        public static WagonSetModel Missing = new WagonSetModel()
        {
            Id = "<unknown>",
            Name = "Missing",
            TrainCars = ImmutableArray<WagonReferenceModel>.Empty
        };

        public static Task<WagonSetModel> GetCore(WagonSetModel wagonSetModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(wagonSetModel, nameof(wagonSetModel));
            return GetCore(wagonSetModel.Id, wagonSetModel.Parent, cancellationToken);
        }

        public static Task<WagonSetModel> GetCore(string consistId, FolderModel folderModel, CancellationToken cancellationToken)
            => GetOrAddCore(consistId, folderModel, cancellationToken);

        public static Task<ImmutableArray<WagonSetModel>> GetWagonSets(FolderModel folderModel, CancellationToken cancellationToken)
            => GetOrAddCollection(folderModel, cancellationToken);

        public static async ValueTask<ImmutableArray<WagonReferenceModel>> GetLocomotives(FolderModel folderModel, CancellationToken cancellationToken)
        {
            ImmutableArray<WagonSetModel> wagonSets = await GetOrAddCollection(folderModel, cancellationToken).ConfigureAwait(false);

            return wagonSets.Select(w => w.Locomotive).Where(l => l != null).Append(WagonReferenceHandler.LocomotiveAny).ToImmutableArray();
        }
    }
}
