namespace Orts.Simulation.Timetables
{
    public abstract class TimetableTrainInfo
    {
        public int TrainNumber { get; set; }
        public string TrainName { get; set; } = string.Empty;
        public int StationPlatformReference { get; set; }               // station platform reference - set to -1 if attaching to static train in dispose command
        public bool Valid { get; set; }
    }
}
