using System;
using Orts.Common;

namespace Orts.ActivityRunner.Viewer3D.Shapes
{
    [Flags]
    public enum ShapeFlags
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
        protected static Viewer viewer;

        internal SharedShape SharedShape;

        internal static void Initialize(Viewer viewer)
        {
            BaseShape.viewer = viewer;
        }

        protected BaseShape(string path, ShapeFlags flags)
        {
            SharedShape = viewer.ShapeManager.Get(path);
            Flags = flags;
        }

        protected ShapeFlags Flags { get; private set; }

        public abstract ref readonly WorldPosition WorldPosition { get; }

        public virtual void Unload()
        { }

        public abstract void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime);

        internal virtual void Mark() => SharedShape.Mark();

        protected static ShapeFlags GetShapeFlags(BaseShape shape)
        {
            return shape.Flags;
        }

    }
}
