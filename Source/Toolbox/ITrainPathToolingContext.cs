using System.Collections.Immutable;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Toolbox
{
    internal interface ITrainPathToolingContext
    {
        bool UseMetricUnits { get; }

        Task<ImmutableArray<PathModelHeader>> GetPaths();
    }
}
