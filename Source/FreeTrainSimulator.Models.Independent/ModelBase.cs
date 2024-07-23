namespace FreeTrainSimulator.Models.Independent
{
    public abstract class ModelBase
    {
        public string Hash { get; init; }
        public string Version { get; set; }
    }
}
