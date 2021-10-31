
using Microsoft.Xna.Framework;

namespace Orts.Common.Input
{
#pragma warning disable CA1052 // Static holder types should be Static or NotInheritable
    public class UserCommandArgs
#pragma warning restore CA1052 // Static holder types should be Static or NotInheritable
    {
        public static UserCommandArgs Empty { get; } = new UserCommandArgs();

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

