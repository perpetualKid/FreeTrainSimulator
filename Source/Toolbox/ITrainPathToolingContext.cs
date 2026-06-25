using System.Collections.Immutable;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime.Track;

namespace FreeTrainSimulator.Toolbox
{
    internal interface ITrainPathToolingContext
    {
        bool UseMetricUnits { get; }

        TrackWorld TrackWorld { get; }

        Task<ImmutableArray<PathModelHeader>> GetPaths();
    }
}
