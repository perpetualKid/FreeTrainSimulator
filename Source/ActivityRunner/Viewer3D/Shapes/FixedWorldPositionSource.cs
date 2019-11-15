using Orts.Common.Position;

namespace Orts.ActivityRunner.Viewer3D.Shapes
{
    /// <summary>
    /// FixedWorldPositionSource uses a readonly (fixed) WorldPosition to act as IWorldPosition source
    /// Used to simplify cases expecting an IWorldPosition source (for moveable content), but only have a const WorldPosition, 
    /// i.e. Animated Scenary objects which are static in position but use <see cref="AnimatedShape">
    /// </summary>
    public class FixedWorldPositionSource : IWorldPosition
    {
        private readonly WorldPosition worldPosition;

        public FixedWorldPositionSource(in WorldPosition worldPosition)
        {
            this.worldPosition = worldPosition;
        }

        public ref readonly WorldPosition WorldPosition => ref worldPosition;
    }
}
