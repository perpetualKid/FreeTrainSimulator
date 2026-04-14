using FreeTrainSimulator.Common.Position;

using MemoryPack;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Models.Track
{
    /// <summary>
    /// A track node representing a junction (switch/turnout) where two or more tracks diverge.
    /// </summary>
    /// <remarks>
    /// Corresponds to a <c>TrJunctionNode</c> in the MSTS <c>.tdb</c> file.
    /// The junction's geometry is defined by a <see cref="ShapeIndex"/> referencing a <see cref="TrackShape"/>.
    /// </remarks>
    [MemoryPackable(GenerateType.VersionTolerant, SerializeLayout.Sequential)]
    public sealed partial record JunctionNode : TrackNodeBase
    {
        /// <summary>Opening angle (in radians) of the diverging route at this junction.</summary>
        public float OpeningAngle { get; init; }

        /// <summary>Index of the main (through) route at this junction. Typically 0 for the straight route.</summary>
        public int MainRoute { get; init; }

        /// <summary>Minimum clearance distance (in meters) from the switch point before the diverging track
        /// is considered safe for passage.</summary>
        public float ClearanceDistance { get; init; }

        /// <summary>Index into <see cref="TrackSectionModel.TrackShapes"/> that defines the 3D shape of this junction.</summary>
        public int ShapeIndex { get; init; }

        [MemoryPackConstructor]
        public JunctionNode(in WorldLocation location, in Tile worldTile, Vector3 direction) : base(location, worldTile, direction)
        {
        }
    }
}