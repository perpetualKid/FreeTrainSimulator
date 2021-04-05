
using Microsoft.Xna.Framework;

namespace Orts.Common.Input
{
    public class UserCommandArgs
    {
        public static UserCommandArgs Empty { get; } = new UserCommandArgs();
    }

    public class PointerCommandArgs : UserCommandArgs
    {
        public Point Position { get; internal set; }
    }

    public class PointerMoveCommandArgs : PointerCommandArgs
    {
        public Vector2 Delta { get; internal set; }
    }

    public class ZoomCommandArgs : PointerCommandArgs
    { 
        public int Delta { get; internal set; }
    }

    public class ModifiableKeyCommandArgs : UserCommandArgs
    {
        public KeyModifiers AdditionalModifiers { get; internal set; }
    }

}

