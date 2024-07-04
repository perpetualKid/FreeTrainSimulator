using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Common.Input
{
    public class UserCommandArgs
    {
        public static UserCommandArgs Empty => new UserCommandArgs();

        public bool Handled { get; set; }
    }

    public class PointerCommandArgs : UserCommandArgs
    {
        public Point Position { get; internal set; }
    }

    public class PointerMoveCommandArgs : PointerCommandArgs
    {
        public Vector2 Delta { get; internal set; }
    }

    public class ScrollCommandArgs : PointerCommandArgs
    {
        public int Delta { get; internal set; }
    }

    public class ModifiableKeyCommandArgs : UserCommandArgs
    {
        public KeyModifiers AdditionalModifiers { get; internal set; }
    }

    public class UserCommandArgs<T> : UserCommandArgs
    {
        public T Value { get; internal set; }
    }
}

