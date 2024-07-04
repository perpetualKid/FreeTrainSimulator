namespace FreeTrainSimulator.Common
{
#pragma warning disable CA1052 // Static holder types should be Static or NotInheritable
    public class ControllerCommandArgs
#pragma warning restore CA1052 // Static holder types should be Static or NotInheritable
    {
        public static ControllerCommandArgs Empty { get; } = new ControllerCommandArgs();
    }

    public class ControllerCommandArgs<T> : ControllerCommandArgs
    {
        public T Value { get; set; }
    }
}
