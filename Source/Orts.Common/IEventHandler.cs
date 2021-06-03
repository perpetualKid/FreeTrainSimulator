namespace Orts.Common
{
    public interface IEventHandler
    {
        void HandleEvent(TrainEvent trainEvent);
        void HandleEvent(TrainEvent trainEvent, object viewer);
    }

}
