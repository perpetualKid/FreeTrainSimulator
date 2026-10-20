using System.Collections.Immutable;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox.PathEditing
{
    internal sealed class TrainPathToolingContext : ITrainPathToolingContext
    {
        private readonly RouteModelHeader route;
        private readonly bool useMetricUnits;

        public bool UseMetricUnits => useMetricUnits;

        public TrackWorld TrackWorld { get; }

        public TrainPathToolingContext(RouteModelHeader route, bool useMetricUnits, TrackWorld trackWorld)
        {
            this.route = route;
            this.useMetricUnits = useMetricUnits;
            TrackWorld = trackWorld;
        }

        public Task<ImmutableArray<PathModelHeader>> GetPaths()
        {
            return route?.GetPaths(System.Threading.CancellationToken.None) ?? Task.FromResult(ImmutableArray<PathModelHeader>.Empty);
        }

        public async Task<ImmutableArray<PathModelHeader>> ValidateAllPaths()
        {
            if (route == null)
                return ImmutableArray<PathModelHeader>.Empty;

            _ = await PathEditor.ValidateRoutePaths(route, TrackWorld, true, System.Threading.CancellationToken.None).ConfigureAwait(false);
            return await route.GetPaths(System.Threading.CancellationToken.None).ConfigureAwait(false);
        }
    }
}
