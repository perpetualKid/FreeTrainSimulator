namespace Orts.Simulation.Signalling
{
    //================================================================================================//
    /// <summary>
    ///
    /// Milepost Object
    ///
    /// </summary>
    //================================================================================================//
    public class Milepost
    {
        public uint TrackItemId { get; }
        public int TrackCircuitReference { get; private set; } = -1;
        public float TrackCircuitOffset { get; private set; }
        public float MilepostValue { get; }

        public Milepost(uint trItemId, float value)
        {
            TrackItemId = trItemId;
            MilepostValue = value;
        }

        internal void SetCircuit(int trackCircuitReference, float trackCircuitOffset)
        {
            TrackCircuitReference = trackCircuitReference;
            TrackCircuitOffset = trackCircuitOffset;
        }
    }

}
