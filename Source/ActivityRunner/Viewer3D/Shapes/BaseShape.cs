using System;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Position;

namespace Orts.ActivityRunner.Viewer3D.Shapes
{
    [Flags]
    public enum ShapeOptions
    {
        None = 0,
        // Shape casts a shadow (scenery objects according to RE setting, and all train objects).
        ShadowCaster = 1,
        // Shape needs automatic z-bias to keep it out of trouble.
        AutoZBias = 2,
        // Shape is an interior and must be rendered in a separate group.
        Interior = 4,
    }

    public abstract class BaseShape: IWorldPosition
    {
        private protected static Viewer viewer;

        internal SharedShape SharedShape;

        internal static void Initialize(Viewer viewer)
        {
            BaseShape.viewer = viewer;
        }

        protected BaseShape(string path, ShapeOptions flags)
        {
            SharedShape = viewer.ShapeManager.Get(path);
            Flags = flags;
        }

        protected ShapeOptions Flags { get; private set; }

        public abstract ref readonly WorldPosition WorldPosition { get; }

        public virtual void Unload()
        { }

        public abstract void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime);

        internal virtual void Mark() => SharedShape.Mark();

        protected static ShapeOptions GetShapeFlags(BaseShape shape)
        {
            return shape?.Flags ?? ShapeOptions.None;
        }

    }
}
