using System.Collections.Immutable;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox
{
    internal sealed class TrainPathToolingContext : ITrainPathToolingContext
    {
        private readonly RouteModelHeader route;
        private readonly bool useMetricUnits;

        public bool UseMetricUnits => useMetricUnits;

        public TrackWorld TrackWorld => RuntimeDataResolver.Instance?.TrackWorld;

        public TrainPathToolingContext(RouteModelHeader route, bool useMetricUnits)
        {
            this.route = route;
            this.useMetricUnits = useMetricUnits;
        }

        public Task<ImmutableArray<PathModelHeader>> GetPaths()
        {
            return route?.GetPaths(System.Threading.CancellationToken.None) ?? Task.FromResult(ImmutableArray<PathModelHeader>.Empty);
        }
    }
}
