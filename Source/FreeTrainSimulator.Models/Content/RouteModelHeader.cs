using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Models.Base;

using MemoryPack;

namespace FreeTrainSimulator.Models.Content
{
    /// <summary>
    /// Header model containing summary information for a route.
    /// Abstracts data originally stored in MSTS route files (<c>.trk</c>).
    /// </summary>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    [ModelResolver(".route")]
    public partial record RouteModelHeader : ModelBase
    {
        private readonly WorldLocation routeStart;

        /// <inheritdoc/>
        public override FolderModel Parent => _parent as FolderModel;
        /// <summary>Human-readable description of the route.</summary>
        public string Description { get; init; }
        /// <summary>Starting world location of the route (longitude/latitude and tile position).</summary>
        public ref readonly WorldLocation RouteStart => ref routeStart;
        /// <summary>Indicates whether the route uses metric units for speed and distance.</summary>
        public bool MetricUnits { get; init; }
        /// <summary>Graphic asset file names indexed by <see cref="GraphicType"/> (e.g. loading screen, route icon).</summary>
        public EnumArray<string, GraphicType> Graphics { get; init; }

        [MemoryPackConstructor]
        protected RouteModelHeader(in WorldLocation routeStart)
        {
            this.routeStart = routeStart;
        }
    }
}
